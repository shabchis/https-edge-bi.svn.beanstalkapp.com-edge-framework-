using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services2
{
	public class ServiceConfiguration
	{
		public Guid ConfigurationID;
		public ServiceInstance RelatedInstance;
		public ServiceProfile Profile;
		public ServiceConfiguration BaseConfiguration;
		public Type ServiceType;
		public string ServiceName;
		public bool IsEnabled;
		public bool IsPublic;
		public ServiceExecutionSettings Settings;
		public Dictionary<string, object> Parameters;
		public List<SchedulingRule> SchedulingRules;
		public ServiceExecutionStatistics Statistics;
	}

	public class ServiceProfile
	{
		public Dictionary<string, object> Parameters;
	}

	public class ServiceExecutionSettings
	{
		public int MaxConcurrentGlobal = 0;
		public int MaxConcurrentPerProfile = 0;
		public int MaxConcurrentPerHost = 0;
		public int MaxEnqueued = 0;
		public int MaxEnqueuedPerProfile = 0;
		public int MaxEnqueuedPerHost = 0;
	}

	public class ServiceExecutionStatistics
	{
		public TimeSpan MaxExecutionTime; // Get this automatically?
		public TimeSpan AverageExecutionTime;
		public TimeSpan MaxWaitTime;
		public TimeSpan AverageWaitTime;
		public double AverageCpuUsage;
		public double AverageMemoryUsage;
	}
}
