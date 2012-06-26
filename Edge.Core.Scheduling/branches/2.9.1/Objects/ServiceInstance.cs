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
		public int ScheduledID;
		public int ID;
		public string ServiceName;
		public int BaseConfigurationID;
		public int ProfileID;
		public DateTime StartTime;
		public DateTime EndTime;
		public int MaxConcurrentPerConfiguration;
		public int MaxCuncurrentPerProfile;
		public int Priority;
		public TimeSpan MaxDeviationBefore;
		public TimeSpan MaxDeviationAfter;
		public TimeSpan ActualDeviation;
		public double Odds;		
		public bool Canceled;		
		public Legacy.ServiceInstance LegacyInstance;		
		public ServiceOutcome Outcome
		{
			get { return this.LegacyInstance.Outcome; }
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
		public string InstanceID { get; set; }
		public string ServiceName { get; set; }
		public int AccountID { get; set; }
		public DateTime SchdeuleStartTime { get; set; }
		public DateTime ScheduleEndTime { get; set; }
		public DateTime ActualStartTime { get; set; }
		public DateTime ActualEndTime { get; set; }
		public double Progress { get; set; }
		public ServiceState State { get; set; }
		public ServiceOutcome Outcome { get; set; }
		public string TargetPeriod { get; set; }
		public Guid ParentInstanceID { get; set; }
		public DateTime BaseScheduleTime { get; set; }
	}
	public struct ServiceHourStruct
	{
		public TimeSpan SuitableHour;
		public SchedulingData Service;
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
