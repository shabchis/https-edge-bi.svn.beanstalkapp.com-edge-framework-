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

namespace Edge.Core.Services
{
	[Serializable]
	public class ServiceInstance: IServiceInfo, ISerializable, IDisposable, ILockable
	{
		#region Instance
		//=================

		internal IServiceConnection Connection;
		bool _autostart = false;
		SchedulingInfo _schedulingInfo;

		public bool ThrowExceptionOnError { get; set; }
		public Guid InstanceID { get; private set; }
		public ServiceConfiguration Configuration { get; private set; }
		public ServiceEnvironment Environment { get; private set; }
		public ServiceInstance ParentInstance { get; private set; }
		IServiceInfo IServiceInfo.ParentInstance { get { return this.ParentInstance; } }
		public SchedulingInfo SchedulingInfo { get { return _schedulingInfo; } set { _lock.Ensure(); _schedulingInfo = value; } }

		internal ServiceInstance(ServiceEnvironment environment, ServiceConfiguration configuration, IServiceInfo parentInstance)
		{
			if (configuration == null)
				throw new ArgumentNullException("configuration");

			if (configuration.ConfigurationLevel == ServiceConfigurationLevel.Instance)
				throw new ArgumentException("Cannot use an existing instance-level configuration to create a new instance. Use instance.Configuration.GetBaseConfiguration(..) instead.", "configuration");

			ThrowExceptionOnError = false;

			this.Environment = environment;
			this.InstanceID = Guid.NewGuid();
			this.Configuration = configuration.Derive(parentInstance);
			//this.ParentInstance = parentInstance;
		}
		
		//=================
		#endregion

		#region State
		//=================

		ServiceState _state = ServiceState.Uninitialized;
		ServiceOutcome _outcome = ServiceOutcome.Unspecified;
		double _progress = 0;
		object _output = null;
		DateTime _timeInitialized = DateTime.MinValue;
		DateTime _timeStarted = DateTime.MinValue;
		DateTime _timeEnded = DateTime.MinValue;

		public event EventHandler StateChanged;
		public event EventHandler OutcomeReported;
		public event EventHandler ProgressReported;
		public event EventHandler OutputGenerated;

		public DateTime TimeInitialized
		{
			get { return _timeInitialized; }
			private set { _timeInitialized = value; }
		}

		public DateTime TimeStarted
		{
			get { return _timeStarted; }
			private set { _timeStarted = value; }
		}
		
		public DateTime TimeEnded
		{
			get { return _timeEnded; }
			private set { _timeEnded = value; }
		}

		public ServiceState State
		{
			get { return _state; }
			private set
			{
				lock (_eventSync)
				{
					_state = value;
					if (StateChanged != null)
						StateChanged(this, EventArgs.Empty);
				}
			}
		}

		public ServiceOutcome Outcome
		{
			get { return _outcome; }
			private set
			{
				lock (_eventSync)
				{
					_outcome = value;
					if (OutcomeReported != null)
						OutcomeReported(this, EventArgs.Empty);
				}
			}
		}

		public object Output
		{
			get { return _output; }
			private set
			{
				lock (_eventSync)
				{
					_output = value;
					if (OutputGenerated != null)
						OutputGenerated(this, EventArgs.Empty);
				}
			}
		}

		public double Progress
		{
			get { return _progress; }
			private set
			{
				lock (_eventSync)
				{
					_progress = value;
					if (ProgressReported != null)
						ProgressReported(this, EventArgs.Empty);
				}
			}
		}

		object _eventSync = new object();
		private void ServiceEventReceived(ServiceEventType eventType, object value)
		{
			switch (eventType)
			{
				case ServiceEventType.StateChanged:
					var ev = (EventValue<ServiceState>)value;

					if (ev.Value == ServiceState.Ready)
						TimeInitialized = ev.Time;
					else if (ev.Value == ServiceState.Running)
						TimeStarted = ev.Time;
					else if (ev.Value == ServiceState.Ended)
						TimeEnded = ev.Time;

					State = ev.Value;

					if (State == ServiceState.Ready && _autostart)
						this.Start();

					break;

				case ServiceEventType.ProgressReported:
					Progress = (double)value;
					break;

				case ServiceEventType.OutputGenerated:
					Output = value;
					break;

				case ServiceEventType.OutcomeReported:

					// Autocomplete progress only if success
					if ((ServiceOutcome)value == ServiceOutcome.Success)
						Progress = 1.0;

					Outcome = (ServiceOutcome)value;
					this.Connection.Dispose();

					break;

				default:
					return;
			}
			
		}

		//=================
		#endregion

		#region Communication
		//=================

		void IDisposable.Dispose()
		{
			this.Connection.Dispose();
		}

		//=================
		#endregion

		#region Control
		//=================
		
		/// <summary>
		/// Finds an appropriate host and asks it to initialize the service.
		/// </summary>
		public void Initialize()
		{
			ServiceExecutionPermission.All.Demand();

			if (Connection != null || State != ServiceState.Uninitialized)
				throw new InvalidOperationException("Service is already initialized.");

			State = ServiceState.Initializing;

			// Get a connection
			try { this.Connection = Environment.AcquireHostConnection(this); }
			catch (Exception ex)
			{
				SetOutcomeException("Environment could not acquire a host for this instance.", ex, true);
				return;
			}

			// Callback
			this.Connection.EventCallback = ServiceEventReceived;

			// Initialize
			// TODO: async
			try { this.Connection.Host.InitializeService(this); }
			catch (Exception ex)
			{
				SetOutcomeException("Host could not initialize this instance.", ex);
				return;
			}
		}

		/// <summary>
		/// Not implemented. Resumes a connection to a running service instance.
		/// </summary>
		public void Connect()
		{
			throw new NotImplementedException("When implemented, this method will simply resume a connection to a running service.");
		}

		/// <summary>
		/// Once the service is initialized, asks the host to start it. If the service is not initialized it will first
		/// be initialized.
		/// </summary>
		public void Start()
		{
			ServiceExecutionPermission.All.Demand();

			// If not initialized, initialize and then progress to Start
			if (Connection == null && State == ServiceState.Uninitialized)
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
			Abort(ServiceOutcome.Aborted);
		}

		internal void Abort(ServiceOutcome outcome)
		{
			// Simple aborting of service without connection
			if (State == ServiceState.Uninitialized)
			{
				Outcome = outcome;
				State = ServiceState.Ended;
			}

			if (State != ServiceState.Running && State != ServiceState.Paused)
				throw new InvalidOperationException("Service can only be aborted when it is in the InProgress or Waiting state.");

			// TODO: async
			this.Connection.Host.AbortService(this.InstanceID);
		}

		[DebuggerNonUserCode]
		private void SetOutcomeException(string message, Exception ex, bool throwEx = false)
		{
			State = ServiceState.Ended;
			Outcome = ServiceOutcome.Failure;
			Output = ex is ServiceException ? ex : new ServiceException(message, ex);

			if (throwEx || ThrowExceptionOnError)
				throw Output as Exception;

			throw ex;
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

		#region Serialization
		//=================

		[SecurityCritical]
		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("InstanceID", InstanceID);
			info.AddValue("Configuration", Configuration);
			info.AddValue("ParentInstanceID", this.ParentInstance == null ? null : (object) this.ParentInstance.InstanceID);
			info.AddValue("SchedulingInfo", SchedulingInfo);
			info.AddValue("Connection", Connection); // this is strictly for internal use only

			info.AddValue("IsLocked", IsLocked);
		}

		private ServiceInstance(SerializationInfo info, StreamingContext context)
		{
			this.InstanceID = (Guid) info.GetValue("InstanceID", typeof(Guid));
			this.Configuration = (ServiceConfiguration)info.GetValue("Configuration", typeof(ServiceConfiguration));
			this.SchedulingInfo = (SchedulingInfo)info.GetValue("SchedulingInfo", typeof(SchedulingInfo));
			this.Connection = (IServiceConnection)info.GetValue("Connection", typeof(IServiceConnection));
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

		[NonSerialized]
		Padlock _lock = new Padlock();
		public bool IsLocked { get { return _lock.IsLocked; } }
		[DebuggerNonUserCode]
		void ILockable.Lock() { ((ILockable)this).Lock(null); }
		[DebuggerNonUserCode]
		void ILockable.Lock(object key)
		{
			_lock.Lock(key);
			((ILockable)this.SchedulingInfo).Lock(key);
		}
		[DebuggerNonUserCode]
		void ILockable.Unlock(object key)
		{
			_lock.Unlock(key);
			((ILockable)this.SchedulingInfo).Unlock(key);
		}

		//=================
		#endregion
	}
}
