using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;
using System.Diagnostics;
using Edge.Core.Services2.Scheduling;

namespace Edge.Core.Services2
{
	[Serializable]
	public class ServiceInstance: IServiceInfo, ISerializable, IDisposable
	{
		#region Instance
		//=================

		internal IServiceConnection Connection;

		bool _owner = false;
		bool _autostart = false;
		Guid _parentInstanceID = Guid.Empty;

		public bool ThrowExceptionOnError { get; set; }

		public Guid InstanceID { get; private set; }
		public ServiceConfiguration Configuration { get; private set; }
		public ServiceEnvironment Environment { get; private set; }
		public ServiceExecutionContext Context { get; private set; }
		public ServiceInstance ParentInstance { get; private set; }
		public SchedulingInfo SchedulingInfo { get; internal set; }
		public ReadOnlyObservableCollection<ServiceInstance> ChildInstances { get; private set; }

		internal ServiceInstance(ServiceEnvironment environment, ServiceConfiguration configuration, ServiceInstance parentInstance)
		{
			if (configuration == null)
				throw new ArgumentNullException("configuration");
			if (configuration.ConfigurationLevel != ServiceConfigurationLevel.Profile)
				throw new ArgumentException("The configuration used to create a new instance must be a profile-level configuration.", "configuration");

			ThrowExceptionOnError = false;

			Environment = environment;
			InstanceID = Guid.NewGuid();
			Configuration = new ServiceConfiguration(ServiceConfigurationLevel.Instance, configuration);
			ParentInstance = parentInstance;
			_owner = true;
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
				_state = value;
				if (StateChanged != null)
					StateChanged(this, EventArgs.Empty);

				if (_state == ServiceState.Ready && _autostart)
					this.Start();
			}
		}

		public ServiceOutcome Outcome
		{
			get { return _outcome; }
			private set
			{
				_outcome = value;
				if (OutcomeReported != null)
					OutcomeReported(this, EventArgs.Empty);
			}
		}

		public object Output
		{
			get { return _output; }
			private set
			{
				_output = value;
				if (OutputGenerated != null)
					OutputGenerated(this, EventArgs.Empty);
			}
		}

		public double Progress
		{
			get { return _progress; }
			private set
			{
				_progress = value;
				if (ProgressReported != null)
					ProgressReported(this, EventArgs.Empty);
			}
		}

		private void ServiceEventReceived(ServiceEventType eventType, object value)
		{
			switch (eventType)
			{
				case ServiceEventType.StateChanged:
					{
						var ev = (EventValue<ServiceState>)value;

						if (ev.Value == ServiceState.Ready)
						{
							TimeInitialized = ev.Time;
						}
						else if (ev.Value == ServiceState.Running)
						{
							TimeStarted = ev.Time;
						}
						else if (ev.Value == ServiceState.Ended)
						{
							TimeEnded = ev.Time;
							this.Connection.Dispose();

							// Autocomplete progress only if success
							if (_outcome == ServiceOutcome.Success)
								Progress = 1.0;
						}

						State = ev.Value;
						break;
					}

				case ServiceEventType.ProgressReported:
					Progress = (double)value;
					break;

				case ServiceEventType.OutputGenerated:
					Output = value;
					break;

				case ServiceEventType.OutcomeReported:
					Outcome = (ServiceOutcome)value;
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
			// TODO: demand ServiceExecutionPermission

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
			try { this.Connection.Host.Initialize(this); }
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
			// If not initialized, initialize and then progress to Start
			if (Connection == null && State == ServiceState.Uninitialized)
			{
				_autostart = true;
				Initialize();
			}
			else if (State != ServiceState.Ready)
				throw new InvalidOperationException("Service can only be started when it is in the Ready state.");

			// Start the service
			// TODO: async
			try { this.Connection.Host.Start(this.InstanceID); }
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

			if (State != ServiceState.Running && State != ServiceState.Waiting)
				throw new InvalidOperationException("Service can only be aborted when it is in the InProgress or Waiting state.");

			// TODO: async
			this.Connection.Host.Abort(this.InstanceID);
		}

		//=================
		#endregion

		[DebuggerNonUserCode]
		private void SetOutcomeException(string message, Exception ex, bool throwEx = false)
		{
			State = ServiceState.Ended;
			Outcome = ServiceOutcome.Error;
			Output = ex is ServiceException ? ex : new ServiceException(message, ex);
			
			if (throwEx || ThrowExceptionOnError)
				throw Output as Exception;

			throw ex;
		}

		public override string ToString()
		{
			return String.Format("{0} (profile: {1}, guid: {2})",
				Configuration.ServiceName,
				Configuration.Profile == null ? "default" : Configuration.Profile.ID.ToString(),
				InstanceID
			);
		}

		#region Serialization
		//=================

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("InstanceID", InstanceID);
			info.AddValue("Configuration", Configuration);
			info.AddValue("Context", Context);
			info.AddValue("ParentInstanceID", ParentInstance == null ? _parentInstanceID : ParentInstance.InstanceID);
			info.AddValue("SchedulingInfo", SchedulingInfo);
			info.AddValue("Connection", Connection); // this is strictly for internal use only
		}

		private ServiceInstance(SerializationInfo info, StreamingContext context)
		{
			this.InstanceID = (Guid) info.GetValue("InstanceID", typeof(Guid));
			this.Configuration = (ServiceConfiguration)info.GetValue("Configuration", typeof(ServiceConfiguration));
			this.Context = (ServiceExecutionContext)info.GetValue("Context", typeof(ServiceExecutionContext));
			this.SchedulingInfo = (SchedulingInfo)info.GetValue("SchedulingInfo", typeof(SchedulingInfo));
			this.Connection = (IServiceConnection)info.GetValue("Connection", typeof(IServiceConnection));
		}

		//=================
		#endregion


		
	}
}
