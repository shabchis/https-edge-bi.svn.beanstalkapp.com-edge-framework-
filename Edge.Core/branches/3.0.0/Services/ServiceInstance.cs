using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;
using System.Diagnostics;
using System.Security;
using System.Runtime.Remoting.Contexts;
using System.ServiceModel;

namespace Edge.Core.Services
{
	[Serializable]
	public class ServiceInstance: ISerializable, IDisposable, ILockable
	{
		#region Instance
		//=================

		internal ServiceConnection Connection;
		bool _autostart = false;
		SchedulingInfo _schedulingInfo = null;
		ServiceStateInfo _stateInfo;

		public bool ThrowExceptionOnError { get; set; }
		public Guid InstanceID { get; private set; }
		public ServiceConfiguration Configuration { get; private set; }
		public ServiceEnvironment Environment { get; private set; }
		public ServiceInstance ParentInstance { get; private set; }
		public SchedulingInfo SchedulingInfo { get { return _schedulingInfo; } set { _lock.Ensure(); _schedulingInfo = value; } }
		
		public double Progress { get { return _stateInfo.Progress; } }
		public ServiceState State { get { return _stateInfo.State; } }
		public ServiceOutcome Outcome { get { return _stateInfo.Outcome; } }
		public DateTime TimeInitialized { get { return _stateInfo.TimeInitialized; } }
		public DateTime TimeStarted { get { return _stateInfo.TimeStarted; } }
		public DateTime TimeEnded { get { return _stateInfo.TimeEnded; } }

		public event EventHandler StateChanged;
		public event EventHandler OutputGenerated;

		internal ServiceInstance(ServiceConfiguration configuration, ServiceEnvironment environment, ServiceInstance parentInstance)
		{
			if (configuration == null)
				throw new ArgumentNullException("configuration");

			if (configuration.ConfigurationLevel == ServiceConfigurationLevel.Instance)
				throw new ArgumentException("Cannot use an existing instance-level configuration to create a new instance. Use instance.Configuration.GetBaseConfiguration(..) instead.", "configuration");

			ThrowExceptionOnError = false;

			this.Environment = environment;
			this.InstanceID = Guid.NewGuid();
			if (parentInstance == null)
			{
				this.Configuration = configuration.Derive(parentInstance.Configuration);
				this.ParentInstance = parentInstance;
			}
		}

		public override string ToString()
		{
			return String.Format("{0} (profile: {1}, guid: {2})",
				Configuration.ServiceName,
				Configuration.Profile == null ? "default" : Configuration.Profile.ProfileID.ToString("N"),
				InstanceID.ToString("N")
			);
		}

		//=================
		#endregion

		#region Communication
		//=================

		public void Connect()
		{
			if (this.Connection != null)
				throw new InvalidOperationException("ServiceInstance is already connected.");

			// Get a connection
			try { this.Connection = Environment.AcquireHostConnection(this.InstanceID); }
			catch (Exception ex)
			{
				throw new ServiceException("Environment could not acquire a connection to the service.", ex);
			}

			// Callback
			this.Connection.StateChangedCallback = OnStateChanged;
			this.Connection.OutputGeneratedCallback = OnOutputGenerated;
			this.Connection.Refresh();
		}

		public void Disconnect()
		{
			if (this.Connection == null)
				throw new InvalidOperationException("ServiceInstance is not connected.");

			this.Connection.Dispose();
		}

		private void OnStateChanged(ServiceStateInfo info)
		{
			_stateInfo = info;
			if (StateChanged != null)
				StateChanged(this, EventArgs.Empty);
		}

		private void OnOutputGenerated(object output)
		{
			if (OutputGenerated != null)
				OutputGenerated(this, new ServiceOutputEventArgs(output));
		}

		void IDisposable.Dispose()
		{
			if (this.Connection != null)
				this.Connection.Dispose();
		}

		//=================
		#endregion

		#region Execution
		//=================
		
		/// <summary>
		/// Finds an appropriate host and asks it to initialize the service.
		/// </summary>
		public void Initialize()
		{
			ServiceExecutionPermission.All.Demand();

			if (State != ServiceState.Uninitialized)
				throw new InvalidOperationException("Service is already initialized.");

			if (Connection != null)
				Connect();

			// Initialize
			try { this.Connection.Host.InitializeService(this); }
			catch (Exception ex)
			{
				SetOutcomeException("Host could not initialize this instance.", ex);
				return;
			}
		}

		/// <summary>
		/// Once the service is initialized, asks the host to start it. If the service is not initialized it will first
		/// be initialized.
		/// </summary>
		public void Start()
		{
			ServiceExecutionPermission.All.Demand();

			if (Connection != null)
				Connect();

			// If not initialized, initialize and then progress to Start
			if (State == ServiceState.Uninitialized)
			{
				_autostart = true;
				Initialize();
				return;
			}
			else if (State != ServiceState.Ready)
				throw new InvalidOperationException("Service can only be started when it is in the Ready state.");

			// Start the service
			try { this.Connection.Host.StartService(this.InstanceID); }
			catch (Exception ex)
			{
				SetOutcomeException("Could not start this instance.", ex);
				return;
			}
		}

		/// <summary>
		/// Aborts the execution of the service.
		/// </summary>
		public void Abort()
		{
			ServiceExecutionPermission.All.Demand();

			if (Connection != null)
				Connect();
	
			// Simple aborting of service without connection
			if (State == ServiceState.Uninitialized)
			{
				Outcome = ServiceOutcome.Canceled;
			}
			else
			{
				if (State != ServiceState.Running && State != ServiceState.Paused)
					throw new InvalidOperationException("Service can only be aborted when it is in the Running or Paused state.");

				this.Connection.Host.AbortService(this.InstanceID);
			}
		}

		//=================
		#endregion

		#region Serialization
		//=================

		[SecurityCritical]
		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("InstanceID", InstanceID);
			info.AddValue("Configuration", Configuration);
			info.AddValue("ParentInstanceID", this.ParentInstance == null ? null : (object) this.ParentInstance.InstanceID);
			info.AddValue("SchedulingInfo", SchedulingInfo);

			info.AddValue("IsLocked", IsLocked);
		}

		private ServiceInstance(SerializationInfo info, StreamingContext context)
		{
			this.InstanceID = (Guid) info.GetValue("InstanceID", typeof(Guid));
			this.Configuration = (ServiceConfiguration)info.GetValue("Configuration", typeof(ServiceConfiguration));
			this.SchedulingInfo = (SchedulingInfo)info.GetValue("SchedulingInfo", typeof(SchedulingInfo));
			this.Environment = new ServiceEnvironment(this.Connection.Host);

			object pid = info.GetValue("ParentInstanceID", typeof(object));
			if (pid != null)
				this.ParentInstance = this.Environment.GetServiceInstance((Guid)pid);

			// Was locked before serialization? Lock 'em up and throw away the key!
			if (info.GetBoolean("IsLocked"))
				((ILockable)this).Lock();
		}

		//=================
		#endregion

		#region ILockable Members
		//=================

		[NonSerialized] Padlock _lock = new Padlock();
		[DebuggerNonUserCode] void ILockable.Lock() { ((ILockable)this).Lock(null); }
		[DebuggerNonUserCode] void ILockable.Lock(object key)
		{
			_lock.Lock(key);
			((ILockable)this.SchedulingInfo).Lock(key);
		}
		[DebuggerNonUserCode] void ILockable.Unlock(object key)
		{
			_lock.Unlock(key);
			((ILockable)this.SchedulingInfo).Unlock(key);
		}

		public bool IsLocked { get { return _lock.IsLocked; } }

		//=================
		#endregion
	}
}
