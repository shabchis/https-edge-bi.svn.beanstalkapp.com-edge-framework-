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
		public bool Canceled;

		private ServiceInstance()
		{
		}

		public ServiceInstanceConfiguration Configuration { get; private set; }
		public Legacy.ServiceInstance LegacyInstance { get; private set; }

		public SchedulingRequest SchedulingRequest { get; set; }
		public double SchedulingAccuracy { get; set; }

		public DateTime ExpectedStartTime
		{
			get { return _expectedStartTime; }
			set { _expectedStartTime = value; this.LegacyInstance.TimeScheduled = value; }
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
				LegacyParentInstanceID = this.LegacyInstance.ParentInstance != null ? this.LegacyInstance.ParentInstance.Guid : Guid.Empty,
				LegacyProgress = this.LegacyInstance.State == Legacy.ServiceState.Ended ? 100 : this.LegacyInstance.Progress
			};
		
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
		public Guid LegacyParentInstanceID { get; set; }
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
	
	public enum ServiceStatus
	{
		Scheduled,
		Running,
		Ended
	}
	
}
