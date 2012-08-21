using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security;
using System.Security.Permissions;
using System.Text;
using Edge.Core.Scheduling;

namespace Edge.Core.Services
{
	public class ServiceExecutionHost : MarshalByRefObject, IServiceHost
	{
		#region Nested classes
		// ========================

		class ServiceRuntimeInfo
		{
			public readonly Guid InstanceID;
			public readonly Service ServiceRef;
			public readonly AppDomain AppDomain;

			public ServiceRuntimeInfo(Guid instanceID, Service serviceRef, AppDomain appDomain)
			{
				InstanceID = instanceID;
				ServiceRef = serviceRef;
				AppDomain = appDomain;
			}
		}

		// ========================
		#endregion

		#region Fields
		// ========================

		public ServiceEnvironment Environment { get; private set; }
		public readonly Guid HostID;
		ServiceExecutionHost _innerHost = null;
		Dictionary<Guid, ServiceRuntimeInfo> _services = new Dictionary<Guid, ServiceRuntimeInfo>();
		Dictionary<int, Guid> _serviceByAppDomain = new Dictionary<int, Guid>();
		//PermissionSet _servicePermissions;
		bool IsWrapper { get { return _innerHost != null; } }

		// ========================
		#endregion 

		#region Methods
		// ========================

		public ServiceExecutionHost(): this(true, Guid.NewGuid())
		{
			ServiceExecutionPermission.All.Demand();
			this.Environment = new ServiceEnvironment(this);
		}

		private ServiceExecutionHost(bool wrapped, Guid hostID)
		{
			this.HostID = hostID;

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

			if (!wrapped)
				return;

			AppDomain domain = AppDomain.CreateDomain(String.Format("Edge host ({0})", HostID));
			_innerHost = (ServiceExecutionHost) domain.CreateInstanceAndUnwrap
			(
				typeof(ServiceExecutionHost).Assembly.FullName,
				typeof(ServiceExecutionHost).FullName,
				false,
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.CreateInstance | System.Reflection.BindingFlags.Instance,
				null,
				new object[] { false, hostID }, // these are ctor arguments
				System.Globalization.CultureInfo.CurrentCulture,
				null
			);
		}

		[OneWay]
		void IServiceHost.InitializeService(ServiceInstance instance)
		{
			if (this.IsWrapper)
			{
				((IServiceHost)_innerHost).InitializeService(instance);
				return;
			}

			// Check if instance ID exists
			lock (_services)
			{
				if (_services.ContainsKey(instance.InstanceID))
					throw new ServiceException(String.Format("Service instance '{0}' is already initialized.", instance.InstanceID));
			}

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

			// Connect the service to the instance
			serviceRef.Connect(instance.Connection);

			// Host it
			lock (_services)
			{
				var info = new ServiceRuntimeInfo(instance.InstanceID, serviceRef, domain);
				_services.Add(instance.InstanceID, info);
				_serviceByAppDomain.Add(domain.Id, info.InstanceID);
			}
	
			// Give the service ref its properties
			serviceRef.Init(this, instance);
		}

		[OneWay]
		void IServiceHost.StartService(Guid instanceID)
		{
			if (this.IsWrapper)
			{
				((IServiceHost)_innerHost).StartService(instanceID);
				return;
			}

			Get(instanceID).ServiceRef.Start();
		}

		/// <summary>
		/// Aborts execution of a running service.
		/// </summary>
		/// <param name="instanceID"></param>
		[OneWay]
		void IServiceHost.AbortService(Guid instanceID)
		{
			if (this.IsWrapper)
			{
				((IServiceHost)_innerHost).AbortService(instanceID);
				return;
			}

			Get(instanceID).ServiceRef.Abort();
		}

		/// <summary>
		/// Disconnects a connection from a running service instance.
		/// </summary>
		[OneWay]
		void IServiceHost.DisconnectService(Guid instanceID, Guid connectionGuid)
		{
			if (this.IsWrapper)
			{
				((IServiceHost)_innerHost).DisconnectService(instanceID, connectionGuid);
				return;
			}

			var info = Get(instanceID, false);
			
			if (info != null)
				info.ServiceRef.Disconnect(connectionGuid);
		}

		[OneWay]
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
			ServiceRuntimeInfo info = Get(instanceID, throwex: false);
			if (info == null)
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

	internal interface IServiceHost
	{
		ServiceEnvironment Environment { get; }
		void InitializeService(ServiceInstance instance);
		void StartService(Guid instanceID);
		void AbortService(Guid instanceID);
		void DisconnectService(Guid instanceID, Guid connectionID);
	}
}
