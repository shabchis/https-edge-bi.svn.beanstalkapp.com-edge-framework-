using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security;
using System.Security.Permissions;
using System.Text;
using Edge.Core.Scheduling;
using System.ServiceModel;

namespace Edge.Core.Services
{
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
	public class ServiceExecutionHost : IServiceHost
	{
		#region Nested classes
		// ========================

		class ServiceRuntimeInfo
		{
			public readonly Guid InstanceID;
			public readonly object ExecutionSync;
			public Service ServiceRef;
			public AppDomain AppDomain;
			public Dictionary<Guid, IServiceConnection> Connections;

			internal ServiceRuntimeInfo(Guid instanceID)
			{
				InstanceID = instanceID;
				ExecutionSync = new object();
				Connections = new Dictionary<Guid, IServiceConnection>();
			}
		}

		// ========================
		#endregion

		#region Fields
		// ========================

		public ServiceEnvironment Environment { get; private set; }
		public readonly string HostName;
		ServiceExecutionHost _innerHost = null;
		Dictionary<Guid, ServiceRuntimeInfo> _services = new Dictionary<Guid, ServiceRuntimeInfo>();
		Dictionary<int, Guid> _serviceByAppDomain = new Dictionary<int, Guid>();
		//PermissionSet _servicePermissions;
		bool IsWrapper { get { return _innerHost != null; } }

		// ========================
		#endregion 

		#region Methods
		// ========================

		public ServiceExecutionHost(string name): this(true, name)
		{
			ServiceExecutionPermission.All.Demand();
			this.Environment = new ServiceEnvironment(this);
		}

		private ServiceExecutionHost(bool isWrapper, string name)
		{
			this.HostName = name;

			/*
			_servicePermissions = new PermissionSet(PermissionState.None);
			_servicePermissions.AddPermission(new ServiceExecutionPermission(ServiceExecutionPermissionFlags.None));
			_servicePermissions.AddPermission(new System.Security.Permissions.FileIOPermission(PermissionState.Unrestricted));
			_servicePermissions.AddPermission(new System.Security.Permissions.ReflectionPermission(PermissionState.Unrestricted));
			_servicePermissions.AddPermission(new System.Security.Permissions.SecurityPermission(
				SecurityPermissionFlag.Assertion |
				SecurityPermissionFlag.Execution |
				SecurityPermissionFlag.ControlAppDomain |
				SecurityPermissionFlag.ControlThread |
				SecurityPermissionFlag.SerializationFormatter
			));
			*/

			if (!isWrapper)
				return;

			AppDomain domain = AppDomain.CreateDomain(String.Format("Edge host - ({0})", HostName));
			_innerHost = (ServiceExecutionHost) domain.CreateInstanceAndUnwrap
			(
				typeof(ServiceExecutionHost).Assembly.FullName,
				typeof(ServiceExecutionHost).FullName,
				false,
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.CreateInstance | System.Reflection.BindingFlags.Instance,
				null,
				new object[] { false, name }, // these are ctor arguments
				System.Globalization.CultureInfo.CurrentCulture,
				null
			);
		}

		void IServiceHost.InitializeServiceInstance(ServiceInstance instance)
		{
			if (this.IsWrapper)
			{
				((IServiceHost)_innerHost).InitializeServiceInstance(instance);
				return;
			}

			ServiceRuntimeInfo runtimeInfo;

			// Check if instance ID exists
			lock (_services)
			{
				if (_services.ContainsKey(instance.InstanceID))
					throw new ServiceException(String.Format("Service instance '{0}' is already initialized.", instance.InstanceID));

				runtimeInfo = new ServiceRuntimeInfo(instance.InstanceID);
				_services.Add(instance.InstanceID, runtimeInfo);
			}

			lock (runtimeInfo.ExecutionSync)
			{

				// Load the app domain, and attach to its events
				AppDomain domain = AppDomain.CreateDomain(
					"Edge service - " + instance.ToString()/*,
				null,
				new AppDomainSetup() { ApplicationBase = Directory.GetCurrentDirectory() },
				_servicePermissions,
				null*/
				);
				domain.DomainUnload += new EventHandler(DomainUnload);

				// Instantiate the service type in the new domain
				Service serviceRef;
				if (instance.Configuration.AssemblyPath == null)
				{
					// No assembly path specified, most likely a core service
					Type serviceType = Type.GetType(instance.Configuration.ServiceType, false);
					if (serviceType == null)
						throw new ServiceException(String.Format("Service type '{0}' could not be found. Please specify AssemblyPath if the service is not in the host directory.", instance.Configuration.ServiceType));

					serviceRef = (Service)domain.CreateInstanceAndUnwrap(serviceType.Assembly.FullName, serviceType.FullName);
				}
				else
				{
					// A 3rd party service
					serviceRef = (Service)domain.CreateInstanceFromAndUnwrap(
						instance.Configuration.AssemblyPath,
						instance.Configuration.ServiceType
					);
				}

				runtimeInfo.ServiceRef = serviceRef;
				runtimeInfo.AppDomain = domain;

				lock (_serviceByAppDomain)
				{
					_serviceByAppDomain.Add(domain.Id, runtimeInfo.InstanceID);
				}

				// Connect the service to the instance
				serviceRef.Connect(instance.Connection);

				// Give the service ref its properties
				serviceRef.Init(this, instance);
			}
		}

		void IServiceHost.StartServiceInstance(Guid instanceID)
		{
			if (this.IsWrapper)
			{
				((IServiceHost)_innerHost).StartServiceInstance(instanceID);
				return;
			}

			ServiceRuntimeInfo runtimeInfo = Get(instanceID);
			lock(runtimeInfo.ExecutionSync)
				runtimeInfo.ServiceRef.Start();
		}

		/// <summary>
		/// Aborts execution of a running service.
		/// </summary>
		/// <param name="instanceID"></param>
		void IServiceHost.AbortServiceInstance(Guid instanceID)
		{
			if (this.IsWrapper)
			{
				((IServiceHost)_innerHost).AbortServiceInstance(instanceID);
				return;
			}

			ServiceRuntimeInfo runtimeInfo = Get(instanceID);
			lock (runtimeInfo.ExecutionSync)
				runtimeInfo.ServiceRef.Abort();
		}

		/// <summary>
		/// Disconnects a connection from a running service instance.
		/// </summary>
		void IServiceHost.DisconnectServiceConnection(Guid instanceID, Guid connectionGuid)
		{
			if (this.IsWrapper)
			{
				((IServiceHost)_innerHost).DisconnectServiceConnection(instanceID, connectionGuid);
				return;
			}

			var runtimeInfo = Get(instanceID, false);
			if (runtimeInfo != null)
			{
				lock (runtimeInfo.Connections)
				{
					runtimeInfo.Connections.Remove(connectionGuid);
				}
			}
		}

		internal void Log(Guid instanceID, LogMessage message)
		{
			// TODO: do something with the log messages
		}

		void DomainUnload(object sender, EventArgs e)
		{
			// Get the service instance ID of the appdomain
			AppDomain appDomain = (AppDomain) sender;
			Guid instanceID;
			if (!_serviceByAppDomain.TryGetValue(appDomain.Id, out instanceID))
				return;

			// Get the runtime in the appdomain
			ServiceRuntimeInfo runtimeInfo = Get(instanceID, throwex: false);
			if (runtimeInfo == null)
				return;

			// Remove the service
			lock (_services)
			{
				_services.Remove(instanceID);
				_serviceByAppDomain.Remove(appDomain.Id);
			}
		}

		ServiceRuntimeInfo Get(Guid instanceID, bool throwex = true)
		{
			ServiceRuntimeInfo service;
			if (!_services.TryGetValue(instanceID, out service))
				if (throwex)
					throw new ServiceException(String.Format("Service instance with ID {0} could not be found in this host.", instanceID));
				else
					return null;
			return service;
		}

		// ========================
		#endregion
	}

	[ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(IServiceConnection))]
	internal interface IServiceHost
	{
		ServiceEnvironment Environment { get; }
		
		[OperationContract(IsOneWay = true)]
		void InitializeServiceInstance(ServiceInstance instance);

		[OperationContract(IsOneWay = true)]
		void StartServiceInstance(Guid instanceID);

		[OperationContract(IsOneWay = true)]
		void ResumeServiceInstance(Guid instanceID);

		[OperationContract(IsOneWay = true)]
		void AbortServiceInstance(Guid instanceID);

		[OperationContract(IsOneWay = true)]
		void DisconnectServiceConnection(Guid instanceID, Guid connectionID);

		[OperationContract]
		IServiceInfo GetServiceInstanceInfo(Guid instanceID);
	}
}
