using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using Legacy = Edge.Core.Services;
using Edge.Core.Configuration;

namespace Edge.Core.Scheduling.Objects
{
	/// <summary>
	/// Date of scheduling
	/// </summary>
	[Serializable]
	public class ServiceInstance
	{
		//public int ID;
		//public int ScheduledID;

		//public string ServiceName;
		private DateTime _expectedStartTime;
		private Legacy.ServiceInstance _legacyInstance;
		public bool Canceled;
		public event EventHandler ProgressReported;
		public event EventHandler StateChanged;
		public event EventHandler OutcomeReported;
		public event EventHandler<ServiceRequestedEventArgs> ChildServiceRequested;

		private ServiceInstance()
		{
		}

		public ServiceInstanceConfiguration Configuration { get; private set; }
		public Legacy.ServiceInstance LegacyInstance
		{
			get
			{
				return _legacyInstance;
			}
			private set
			{
				value.ChildServiceRequested -= new EventHandler<ServiceRequestedEventArgs>(LegacyInstance_ChildServiceRequested);
				value.ChildServiceRequested += new EventHandler<ServiceRequestedEventArgs>(LegacyInstance_ChildServiceRequested);
				value.OutcomeReported -= new EventHandler(LegacyInstance_OutcomeReported);
				value.OutcomeReported += new EventHandler(LegacyInstance_OutcomeReported);
				value.StateChanged -= new EventHandler<ServiceStateChangedEventArgs>(LegacyInstance_StateChanged);
				value.StateChanged += new EventHandler<ServiceStateChangedEventArgs>(LegacyInstance_StateChanged);
				value.ProgressReported -= new EventHandler(LegacyInstance_ProgressReported);
				value.ProgressReported += new EventHandler(LegacyInstance_ProgressReported);
				_legacyInstance = value;

			}
		}

		public SchedulingRequest SchedulingRequest { get; set; }
		public double SchedulingAccuracy { get; set; }
		public DateTime ExpectedStartTime
		{
			get { return _expectedStartTime; }
			set { _expectedStartTime = value; this.LegacyInstance.TimeScheduled = value; }
		}
		public Double Progress
		{
			get
			{
				return _legacyInstance.Progress;
			}
		}

		public DateTime ExpectedEndTime { get; set; }

		public ServiceOutcome Outcome
		{
			get { return this.LegacyInstance.Outcome; }
		}

		public TimeSpan ActualDeviation
		{
			get { return this.ExpectedStartTime.Subtract(this.SchedulingRequest.RequestedTime); }
		}

		public static ServiceInstance FromLegacyInstance(Legacy.ServiceInstance legacyInstance, ServiceConfiguration configuration, Profile profile = null)
		{
			var serviceInstance = new ServiceInstance()
			{
				Configuration = ServiceInstanceConfiguration.FromLegacyConfiguration(legacyInstance, configuration, profile ?? configuration.Profile),
				LegacyInstance = legacyInstance
			};

			serviceInstance.Configuration.Instance = serviceInstance;
			return serviceInstance;
		}

		void LegacyInstance_ProgressReported(object sender, EventArgs e)
		{
			ProgressReported(this, new EventArgs());
		}

		void LegacyInstance_StateChanged(object sender, ServiceStateChangedEventArgs e)
		{
			StateChanged(this, new EventArgs());
		}

		void LegacyInstance_OutcomeReported(object sender, EventArgs e)
		{
			OutcomeReported(this, new EventArgs());
		}

		void LegacyInstance_ChildServiceRequested(object sender, ServiceRequestedEventArgs e)
		{
			ChildServiceRequested(this, e);
		}

		public ServiceInstanceInfo GetInfo()
		{
			return new ServiceInstanceInfo()
			{
				LegacyInstanceGuid = this.LegacyInstance.Guid,
				AccountID = Convert.ToInt32(this.Configuration.Profile.Settings["AccountID"]),
				LegacyInstanceID = this.LegacyInstance.InstanceID.ToString(),
				LegacyOutcome = this.LegacyInstance.Outcome,
				ScheduleStartTime = this.ExpectedStartTime,
				ScheduleEndTime = this.ExpectedEndTime,
				BaseScheduleTime = this.SchedulingRequest.RequestedTime,
				LegacyActualStartTime = this.LegacyInstance.TimeStarted,
				LegacyActualEndTime = this.LegacyInstance.TimeEnded,
				ServiceName = this.Configuration.Name,
				LegacyState = this.LegacyInstance.State,
				// ScheduledID =this.ScheduledID,
				Options = this.LegacyInstance.Configuration.Options,
				LegacyParentInstanceGuid = this.LegacyInstance.ParentInstance != null ? this.LegacyInstance.ParentInstance.Guid : Guid.Empty,
				LegacyProgress = this.LegacyInstance.State == Legacy.ServiceState.Ended ? 100 : this.LegacyInstance.Progress
			};

		}

		public void Initialize()
		{
			_legacyInstance.Initialize();
		}

		public ServiceState State
		{
			get
			{
				return _legacyInstance.State;
			}


		}

		public void Start()
		{
			_legacyInstance.Start();
		}
	}
	/// <summary>
	/// service-hour 
	/// </summary>
	/// 
	public class ServiceInstanceInfo
	{
		public Guid LegacyInstanceGuid { get; set; }
		public int ScheduledID { get; set; }
		public string LegacyInstanceID { get; set; }
		public string ServiceName { get; set; }
		public int AccountID { get; set; }
		public DateTime ScheduleStartTime { get; set; }
		public DateTime ScheduleEndTime { get; set; }
		public DateTime LegacyActualStartTime { get; set; }
		public DateTime LegacyActualEndTime { get; set; }
		public double LegacyProgress { get; set; }
		public ServiceState LegacyState { get; set; }
		public ServiceOutcome LegacyOutcome { get; set; }
		public Edge.Core.SettingsCollection Options { get; set; }
		public Guid LegacyParentInstanceGuid { get; set; }
		public DateTime BaseScheduleTime { get; set; }
	}
	public struct ServiceHour
	{
		public TimeSpan SuitableHour;
		public SchedulingRequest Service;
	}
	public class AccountServiceInformation
	{
		public int ID { get; set; }
		public string AccountName { get; set; }
		public List<string> Services { get; set; }
	}
	public class ChildServiceEventArgs : EventArgs
	{
		public ChildServiceEventArgs(ServiceInstance serviceInstance)
		{

		}
		public ServiceInstance RequestedService { get;private  set; }

	}

	public enum ServiceStatus
	{
		Scheduled,
		Running,
		Ended
	}

}
