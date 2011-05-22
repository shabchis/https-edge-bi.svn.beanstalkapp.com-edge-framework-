using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Edge.Core.Services2.Scheduling;

namespace Edge.Core.Services2
{
	[Serializable]
	public class ServiceConfiguration
	{
		public Guid ConfigurationID;
		public ServiceConfigurationLevel ConfigurationLevel { get; private set; }
		public ServiceConfiguration BaseConfiguration { get; private set; }
		public ServiceProfile Profile { get { throw new NotImplementedException(); } }
		public string AssemblyPath;
		public string ServiceType;
		public string ServiceName;
		public bool IsEnabled;
		public bool IsPublic;
		public ServiceExecutionLimits Limits;
		public Dictionary<string, object> Parameters;
		public List<SchedulingRule> SchedulingRules;
		public ServicePriority Priority;

		public ServiceConfiguration()
		{
		}

		internal ServiceConfiguration(ServiceConfigurationLevel level, ServiceConfiguration baseConfig)
		{
			// TODO: inherit values etc.
		}

		internal ServiceConfiguration ByLevel(ServiceConfigurationLevel level)
		{
			ServiceConfiguration target = null;

			switch(level)
			{
				case ServiceConfigurationLevel.Instance:
					target = this.ConfigurationLevel == ServiceConfigurationLevel.Instance ? this : null;
					break;

				case ServiceConfigurationLevel.Profile:
					if (this.ConfigurationLevel == ServiceConfigurationLevel.Profile)
						target = this;
					else if (this.ConfigurationLevel == ServiceConfigurationLevel.Instance)
						target = this.BaseConfiguration;
					else
						target = null;
					break;

				case ServiceConfigurationLevel.Global:
					target = this;
					while (target.ConfigurationLevel != ServiceConfigurationLevel.Global && target.BaseConfiguration != null)
						target = target.BaseConfiguration;
					break;
			}
			return target;
		}

		public ServiceExecutionStatistics GetStatistics(int _percentile)
		{
			throw new NotImplementedException();
		}
	}

	public enum ServiceConfigurationLevel
	{
		Global,
		Profile,
		Instance
	}

	[Serializable]
	public class ServiceProfile
	{
		public Guid ID;
		public Dictionary<string, object> Parameters;
	}

	[Serializable]
	public class ServiceExecutionLimits
	{
		public int MaxConcurrentGlobal = 0;
		public int MaxConcurrentPerProfile = 0;
		public int MaxConcurrentPerHost = 0;
		public int MaxEnqueued = 0;
		public int MaxEnqueuedPerProfile = 0;
		public int MaxEnqueuedPerHost = 0;
	}

	[Serializable]
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
