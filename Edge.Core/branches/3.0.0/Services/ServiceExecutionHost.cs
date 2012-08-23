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
	public class ServiceExecutionHost : MarshalByRefObject, IServiceExecutionHost
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
		public string HostName { get; private set; }
		Dictionary<Guid, ServiceRuntimeInfo> _services = new Dictionary<Guid, ServiceRuntimeInfo>();
		Dictionary<int, Guid> _serviceByAppDomain = new Dictionary<int, Guid>();
		//PermissionSet _servicePermissions;

		// ========================
		#endregion 

		#region Methods
		// ========================

		public ServiceExecutionHost(string name)
		{
			ServiceExecutionPermission.All.Demand();

			this.HostName = name;
			this.Environment = new ServiceEnvironment(this);

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
		}

		void IServiceExecutionHost.InitializeService(ServiceInstance instance)
		{
			ServiceRuntimeInfo runtimeInfo;

			// Check if instance ID exists
			lock (_services)
			{
				if (_services.ContainsKey(instance.InstanceID))
					throw new ServiceException(String.Format("Service instance '{0}' is already initialized.", instance.InstanceID));

				runtimeInfo = new ServiceRuntimeInfo(instance.InstanceID);
				runtimeInfo.Connections.Add(instance.Connection.Guid, instance.Connection);
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
				};

				// Give the service ref its properties
				serviceRef.Init(this, instance);
			}
		}

		void IServiceExecutionHost.StartService(Guid instanceID)
		{
			ServiceRuntimeInfo runtimeInfo = Get(instanceID);
			lock(runtimeInfo.ExecutionSync)
				runtimeInfo.ServiceRef.Start();
		}

		void IServiceExecutionHost.ResumeService(Guid instanceID)
		{
			ServiceRuntimeInfo runtimeInfo = Get(instanceID);
			lock (runtimeInfo.ExecutionSync)
				runtimeInfo.ServiceRef.Resume();
		}

		/// <summary>
		/// Aborts execution of a running service.
		/// </summary>
		/// <param name="instanceID"></param>
		void IServiceExecutionHost.AbortService(Guid instanceID)
		{
			ServiceRuntimeInfo runtimeInfo = Get(instanceID);
			lock (runtimeInfo.ExecutionSync)
				runtimeInfo.ServiceRef.Abort();
		}

		/// <summary>
		/// Disconnects a connection from a running service instance.
		/// </summary>
		void IServiceExecutionHost.CloseConnection(Guid instanceID, Guid connectionGuid)
		{
			var runtimeInfo = Get(instanceID, false);
			if (runtimeInfo != null)
			{
				lock (runtimeInfo.Connections)
					runtimeInfo.Connections.Remove(connectionGuid);
			}
		}

		void IServiceExecutionHost.RefreshConnection(Guid instanceID, Guid connectionGuid)
		{
			var runtimeInfo = Get(instanceID, true);
			lock (runtimeInfo.Connections)
			{
				foreach (IServiceConnection connection in runtimeInfo.Connections.Values)
					connection.ReceiveState(runtimeInfo.ServiceRef.StateInfo);
			}
		}

		internal void NotifyState(Guid instanceID, ServiceStateInfo stateInfo)
		{
			var runtimeInfo = Get(instanceID, true);
			lock (runtimeInfo.Connections)
			{
				foreach (IServiceConnection connection in runtimeInfo.Connections.Values)
					connection.ReceiveState(stateInfo);
			}
		}

		internal void NotifyOutput(Guid instanceID, object output)
		{
			var runtimeInfo = Get(instanceID, true);
			lock (runtimeInfo.Connections)
			{
				foreach (IServiceConnection connection in runtimeInfo.Connections.Values)
					connection.ReceiveOutput(output);
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
	internal interface IServiceExecutionHost
	{
		string HostName { get; }
		
		[OperationContract(IsOneWay = true)]
		void InitializeService(ServiceInstance instance);

		[OperationContract(IsOneWay = true)]
		void StartService(Guid instanceID);

		[OperationContract(IsOneWay = true)]
		void ResumeService(Guid instanceID);

		[OperationContract(IsOneWay = true)]
		void AbortService(Guid instanceID);

		[OperationContract(IsOneWay = true)]
		void CloseConnection(Guid instanceID, Guid connectionID);
	}
}
