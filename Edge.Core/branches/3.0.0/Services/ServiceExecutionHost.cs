using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Channels;
using Edge.Core.Configuration;
using System.ServiceModel.Description;

namespace Edge.Core.Services
{
	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
	public class ServiceExecutionHost : MarshalByRefObject, IServiceExecutionHost, IDisposable
	{
		#region Nested classes
		// ========================

		class ServiceRuntimeInfo
		{
			public readonly Guid InstanceID;
			public readonly object ExecutionSync;
			public readonly object DbSaveSync;
			public Service ServiceRef;
			public AppDomain AppDomain;
			public Dictionary<Guid, IServiceConnection> Connections;

			public Guid ParentInstanceID;
			public ServiceStateInfo? StateInfo;
			public SchedulingInfo SchedulingInfo;
			public ServiceConfiguration Configuration;

			internal ServiceRuntimeInfo(Guid instanceID)
			{
				InstanceID = instanceID;
				ExecutionSync = new object();
				DbSaveSync = new object();
				Connections = new Dictionary<Guid, IServiceConnection>();
			}
		}

		// ========================
		#endregion

		#region Fields
		// ========================

		public ServiceEnvironment Environment { get; private set; }
		public string HostName { get; private set; }
		public Guid HostGuid { get; private set; }

		Dictionary<Guid, ServiceRuntimeInfo> _services = new Dictionary<Guid, ServiceRuntimeInfo>();
		Dictionary<int, Guid> _serviceByAppDomain = new Dictionary<int, Guid>();
		internal WcfHost WcfHost { get; private set; }

		// ========================
		#endregion

		#region Methods
		// ========================

		public ServiceExecutionHost(string name, ServiceEnvironment environment)
		{
			ServiceExecutionPermission.All.Demand();

			this.HostName = name;
			this.HostGuid = Guid.NewGuid();
			this.Environment = environment;

			this.WcfHost = new WcfHost(this.Environment, this);

			ServiceEndpoint[] rawEndpoints = this.WcfHost.Description.Endpoints.ToArray();
			this.WcfHost.Description.Endpoints.Clear();
			foreach (ServiceEndpoint endpoint in rawEndpoints)
			{
				endpoint.Address = new EndpointAddress(new Uri(endpoint.Address.Uri.ToString().Replace("{hostName}", name)));
				this.WcfHost.AddServiceEndpoint(endpoint);
			}

			this.Environment.RegisterHost(this);

			// Open the listener
			WcfHost.Open();

			#region Code Access Security (disabled)
			/*
			
			// Move following line outside this function
			// PermissionSet _servicePermissions;
			
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
			#endregion
		}

		void IServiceExecutionHost.Connect(Guid instanceID, Guid connectionGuid)
		{
			ServiceRuntimeInfo runtimeInfo;

			// Check if instance ID exists
			lock (_services)
			{
				if (!_services.TryGetValue(instanceID, out runtimeInfo))
					runtimeInfo = new ServiceRuntimeInfo(instanceID);

				runtimeInfo.Connections.Add(connectionGuid, OperationContext.Current.GetCallbackChannel<IServiceConnection>());
				_services.Add(instanceID, runtimeInfo);
			}
		}

		/// <summary>
		/// Disconnects a connection from a running service instance.
		/// </summary>
		void IServiceExecutionHost.Disconnect(Guid instanceID, Guid connectionGuid)
		{
			var runtimeInfo = Get(instanceID, false);
			if (runtimeInfo != null)
			{
				lock (runtimeInfo.Connections)
				{
					runtimeInfo.Connections.Remove(connectionGuid);
				}
			}
		}

		void IServiceExecutionHost.InitializeService(ServiceConfiguration config, SchedulingInfo schedulingInfo, Guid instanceID, Guid parentInstanceID, Guid connectionGuid)
		{
			if (String.IsNullOrEmpty(config.ServiceClass))
				throw new ServiceException("ServiceConfiguration.ServiceClass cannot be empty.");

			ServiceRuntimeInfo runtimeInfo = Get(instanceID);
			lock (runtimeInfo.ExecutionSync)
			{
				if (runtimeInfo.StateInfo != null && runtimeInfo.StateInfo.Value.State != ServiceState.Uninitialized)
					throw new ServiceException("Service is already initialized.");

				// Save init data for later
				runtimeInfo.ParentInstanceID = parentInstanceID;
				runtimeInfo.Configuration = config;
				runtimeInfo.SchedulingInfo = schedulingInfo;
				UpdateState(instanceID, new ServiceStateInfo() { State = ServiceState.Initializing });

				// Load the app domain, and attach to its events
				AppDomain domain = AppDomain.CreateDomain(
					String.Format("Edge service - {0} ({1})", config.ServiceName, instanceID)
				);
				domain.DomainUnload += new EventHandler(DomainUnload);


				// Instantiate the service type in the new domain
				Service serviceRef;
				if (config.AssemblyPath == null)
				{
					Type serviceType = Type.GetType(config.ServiceClass, false);

					if (serviceType == null)
						serviceType = Type.GetType(String.Format("Edge.Core.Services.{0}.{0}Service", config.ServiceClass), false);

					if (serviceType == null)
						throw new ServiceException(String.Format("Service type '{0}' could not be found. Please specify AssemblyPath if the service is not in the host directory.", config.ServiceClass));

					serviceRef = (Service)domain.CreateInstanceAndUnwrap(serviceType.Assembly.FullName, serviceType.FullName);
				}
				else
				{
					// A 3rd party service
					serviceRef = (Service)domain.CreateInstanceFromAndUnwrap(
						config.AssemblyPath,
						config.ServiceClass
					);
				}

				runtimeInfo.ServiceRef = serviceRef;
				runtimeInfo.AppDomain = domain;

				lock (_serviceByAppDomain)
				{
					_serviceByAppDomain.Add(domain.Id, runtimeInfo.InstanceID);
				};

				// Give the service ref its properties
				serviceRef.Init(this, this.Environment.EnvironmentConfiguration, config, schedulingInfo, instanceID, parentInstanceID);
			}
		}

		void IServiceExecutionHost.StartService(Guid instanceID)
		{
			ServiceRuntimeInfo runtimeInfo = Get(instanceID);
			lock (runtimeInfo.ExecutionSync)
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


		void IServiceExecutionHost.NotifyState(Guid instanceID)
		{
			UpdateState(instanceID, null, false, false);
		}

		internal void UpdateState(Guid instanceID, ServiceStateInfo? stateInfo, bool save = true, bool throwEx = true)
		{
			var runtimeInfo = Get(instanceID, throwEx);
			if (runtimeInfo == null)
				return;

			if (stateInfo != null)
				runtimeInfo.StateInfo = stateInfo.Value;

			if (runtimeInfo.StateInfo == null)
				return;

			lock (runtimeInfo.Connections)
			{
				foreach (IServiceConnection connection in runtimeInfo.Connections.Values)
					connection.ReceiveState(runtimeInfo.StateInfo.Value);
			}

			if (save)
			{
				lock (runtimeInfo.DbSaveSync)
				{
					Environment.SaveServiceInstance(
						this,
						runtimeInfo.InstanceID,
						runtimeInfo.ParentInstanceID,
						runtimeInfo.Configuration,
						runtimeInfo.StateInfo.Value,
						runtimeInfo.SchedulingInfo
					);
				}
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
			AppDomain appDomain = (AppDomain)sender;
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

		internal ServiceInstance GetServiceInstance(Guid instanceID)
		{
			throw new NotImplementedException();
		}

		// ========================
		#endregion

		#region IDisposable Members
		// ========================
		void IDisposable.Dispose()
		{
			// Close WCF host				
			if (WcfHost != null)
			{
				if (WcfHost.State == CommunicationState.Faulted)
					WcfHost.Abort();
				else
					WcfHost.Close();
			}

			Environment.UnregisterHost(this);
		}
		// ========================
		#endregion
	}



}
