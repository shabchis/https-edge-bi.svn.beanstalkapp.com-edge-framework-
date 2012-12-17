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
using Edge.Core.Utilities;

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

		bool _disposed = false;

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

			// Start the logger
			Edge.Core.Utilities.Log.Start();

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
			EnsureNotDisposed();

			ServiceRuntimeInfo runtimeInfo;

			// Check if instance ID exists
			lock (_services)
			{
				if (!_services.TryGetValue(instanceID, out runtimeInfo))
				{
					runtimeInfo = new ServiceRuntimeInfo(instanceID);
					_services.Add(instanceID, runtimeInfo);
				}
			}

			lock(runtimeInfo.Connections)
				runtimeInfo.Connections.Add(connectionGuid, OperationContext.Current.GetCallbackChannel<IServiceConnection>());
			
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

				// When no more connections exist to an uninitialized service, clear the runtime info since it doesn't retain useful information
				if (runtimeInfo.StateInfo == null && runtimeInfo.Connections.Count == 0)
				{
					lock (_services)
					{
						_services.Remove(instanceID);
					}

				}
			}
		}

		void IServiceExecutionHost.InitializeService(ServiceConfiguration config, SchedulingInfo schedulingInfo, Guid instanceID, Guid parentInstanceID, Guid connectionGuid)
		{
			EnsureNotDisposed();

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

				var setup = new AppDomainSetup()
				{
					ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
				};

				// Load the app domain, and attach to its events
				AppDomain domain = AppDomain.CreateDomain(
					friendlyName: String.Format("Edge service - {0} ({1})", config.ServiceName, instanceID),
                    securityInfo: null,
                    info: setup
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
				try { serviceRef.Init(this, this.Environment.EnvironmentConfiguration, config, schedulingInfo, instanceID, parentInstanceID); }
				catch (AppDomainUnloadedException)
				{
					HostLog("Service was killed while trying to initialize.", null, LogMessageType.Warning);
				}
			}
		}

		void IServiceExecutionHost.StartService(Guid instanceID)
		{
			EnsureNotDisposed();

			ServiceRuntimeInfo runtimeInfo = Get(instanceID);
			lock (runtimeInfo.ExecutionSync)
			{
				try { runtimeInfo.ServiceRef.Start(); }
				catch (AppDomainUnloadedException)
				{
					HostLog("Service was killed while trying to start.", null, LogMessageType.Warning);
				}
			}
		}

		void IServiceExecutionHost.ResumeService(Guid instanceID)
		{
			EnsureNotDisposed();

			ServiceRuntimeInfo runtimeInfo = Get(instanceID);
			lock (runtimeInfo.ExecutionSync)
			{
				try { runtimeInfo.ServiceRef.Resume(); }
				catch (AppDomainUnloadedException)
				{
					HostLog("Service was killed while trying to resume.", null, LogMessageType.Warning);
				}
			}
		}

		/// <summary>
		/// Aborts execution of a running service.
		/// </summary>
		/// <param name="instanceID"></param>
		void IServiceExecutionHost.AbortService(Guid instanceID)
		{
			EnsureNotDisposed();

			ServiceRuntimeInfo runtimeInfo = Get(instanceID);
			lock (runtimeInfo.ExecutionSync)
			{
				if (runtimeInfo.StateInfo == null)
				{
					// Special case to notify connections (ServiceInstance objects) that a service has been canceled and will never run
					lock (runtimeInfo.Connections)
					{
						foreach (IServiceConnection connection in runtimeInfo.Connections.Values)
							connection.ReceiveState(new ServiceStateInfo() {State = ServiceState.Ended, Outcome = ServiceOutcome.Canceled});

						// candidate for DeadLock
						lock (_services)
							_services.Remove(instanceID);
					}
				}
				else
				{
					// Abort a running service
					try
					{
						runtimeInfo.ServiceRef.Abort();
					}
					catch (AppDomainUnloadedException)
					{
						HostLog("Service was killed while trying to abort.", null, LogMessageType.Warning);
					}
				}
			}
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


		internal void InstanceLog(Guid instanceID, Guid profileID, string serviceName, string contextInfo, string message, Exception ex, LogMessageType messageType)
		{
			// Get the runtime in the appdomain
			ServiceRuntimeInfo runtimeInfo = Get(instanceID, throwex: false);

			var entry = new LogMessage(
				source: serviceName,
				contextInfo: contextInfo,
				message: message,
				ex: ex,
				messageType: messageType
			)
			{
				ServiceInstanceID = instanceID,
				ServiceProfileID = profileID
			};

			Log.Write(entry);
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

			#region Probably not necessary - TBD 
			// http://stackoverflow.com/questions/8165398/do-i-need-to-close-and-or-dispose-callback-channels-acquired-through-operationco
			
			// Dispose of open channels
			/*
			lock (runtimeInfo.Connections)
			{
				foreach (IServiceConnection connection in runtimeInfo.Connections.Values)
				{
					try { connection.Dispose(); }
					catch (Exception ex)
					{
						HostLog("Forcing the connection to close caused an exception.", ex, LogMessageType.Warning);
					}
				}
			}
			*/
			#endregion
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

		void HostLog(string message, Exception ex, LogMessageType messageType = LogMessageType.Error)
		{
			Log.Write(string.Format("Host: {0}", this.HostName), this.HostGuid.ToString(), message, ex, messageType);
		}

		// ========================
		#endregion

		#region Disposing
		// ========================

		void EnsureNotDisposed()
		{
			if (_disposed)
				throw new ObjectDisposedException("The host has been disposed and cannot be used any more.");
		}

		void IDisposable.Dispose()
		{
			_disposed = true;
			
			foreach (ServiceRuntimeInfo runtimeInfo in _services.Values.ToArray())
			{
				if (runtimeInfo.ServiceRef != null)
				{
					try { runtimeInfo.ServiceRef.Kill(); }
					catch (AppDomainUnloadedException) { }
				}
			}

			// Close WCF host				
			if (WcfHost != null)
			{
				if (WcfHost.State == CommunicationState.Faulted)
					WcfHost.Abort();
				else
					WcfHost.Close();
			}

			Edge.Core.Utilities.Log.Stop();

			Environment.UnregisterHost(this);
		}


		// ========================
		#endregion
	}



}
