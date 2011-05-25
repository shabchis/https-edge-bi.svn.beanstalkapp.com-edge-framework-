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
		string _assemblyPath;
		string _serviceType;
		string _serviceName;
		bool _isEnabled;
		bool _isPublic;

		public Guid ConfigurationID { get; private set; }
		public ServiceConfigurationLevel ConfigurationLevel { get; private set; }
		public ServiceConfiguration ParentConfiguration { get; private set; }
		public ServiceProfile Profile { get; private set; }
		public string AssemblyPath;
		public string ServiceType;
		public string ServiceName;
		public bool IsEnabled;
		public bool IsPublic;
		public ServicePriority Priority;
		public ServiceExecutionLimits Limits {get; private set;}
		public IDictionary<string, object> Parameters;
		public IList<SchedulingRule> SchedulingRules { get; private set; }

		public ServiceConfiguration TemplateConfiguration
		{
			get { return ByLevel(ServiceConfigurationLevel.Template); }
		}

		public ServiceConfiguration ProfileConfiguration
		{
			get { return ByLevel(ServiceConfigurationLevel.Profile); }
		}

		internal const string LockedExceptionMessage = "The configuration object cannot be modified at this point.";

		public ServiceConfiguration(ServiceConfiguration baseConfiguration = null)
		{
		}

		internal ServiceConfiguration(ServiceConfiguration baseConfiguration, ServiceConfigurationLevel level)
		{
			this.ConfigurationID = Guid.NewGuid();
			this.ConfigurationLevel = level;
			this.Limits = new ServiceExecutionLimits();

			if (baseConfiguration == null)
				return;

			this.Profile = baseConfiguration.Profile;
			this.AssemblyPath = baseConfiguration.AssemblyPath;
			this.ServiceType = baseConfiguration.ServiceType;
			this.ServiceName = baseConfiguration.ServiceName;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="level">Indicates the configuration level (template, profile, or instance) to find.</param>
		/// <param name="search">Indicates whether the lowest ancestor or the highest ancestor should be found of the given type. (Highest not yet implemented).</param>
		/// <returns></returns>
		internal ServiceConfiguration ByLevel(ServiceConfigurationLevel level, ServiceConfigurationLevelSearch search = ServiceConfigurationLevelSearch.Lowest)
		{
			// TODO: implement ByLevel with search = Highest
			if (search == ServiceConfigurationLevelSearch.Highest)
				throw new NotImplementedException("Only the lowest level can be found at the moment.");

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
						target = this.ParentConfiguration;
					else
						target = null;
					break;

				case ServiceConfigurationLevel.Template:
					target = this;
					while (target.ConfigurationLevel != ServiceConfigurationLevel.Template && target.ParentConfiguration != null)
						target = target.ParentConfiguration;
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
		Template,
		Profile,
		Instance
	}

	internal enum ServiceConfigurationLevelSearch
	{
		Highest,
		Lowest
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
