using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;
using Legacy = Edge.Core.Services; 


namespace Edge.Core.Scheduling.Objects
{
	public class ServiceConfiguration
	{
		private int _maxConcurrent = 1;
		private int _maxCuncurrentPerProfile = 1;
        private Guid _guid;
		public int ID;
		public ServiceConfiguration BaseConfiguration;			
		public string Name;
		public Profile SchedulingProfile;
		public List<SchedulingRule> SchedulingRules=new List<SchedulingRule>();
		public bool Scheduled = false;
		public TimeSpan AverageExecutionTime=new TimeSpan(0,30,0);
		public TimeSpan MaxExecutionTime = new TimeSpan(0,60, 0);
		public ServiceElement LegacyConfiguration;
		public int Priority;

        public ServiceConfiguration()
        {
            _guid = new Guid();
        }

        public override int GetHashCode()
        {
            return _guid.GetHashCode();
        }

		public int MaxConcurrent
		{
			get { return _maxConcurrent; }
			set
			{
				if (value != 0)
					_maxConcurrent = value;
				else
					value = 999;
			}
		}

		public int MaxConcurrentPerProfile
		{
			get { return _maxCuncurrentPerProfile; }
			set
			{
				if (value != 0)
					_maxCuncurrentPerProfile = value;
				else
					value = 999;
			}
		}

		public static ServiceConfiguration FromLegacyConfiguration(NamedConfigurationElement legacyConfiguration, ServiceConfiguration baseConfiguration = null, Profile profile = null, Dictionary<string, string> options = null)
		{
			return FromLegacyConfiguration<ServiceConfiguration>(legacyConfiguration, baseConfiguration, profile, options);
		}

		protected static T FromLegacyConfiguration<T>(NamedConfigurationElement legacyConfiguration, ServiceConfiguration baseConfiguration = null, Profile profile = null, Dictionary<string, string> options = null) where T: ServiceConfiguration, new()
		{
			if (legacyConfiguration is AccountServiceElement || legacyConfiguration is WorkflowStepElement || legacyConfiguration is ActiveServiceElement)
			{
				if (baseConfiguration == null)
					throw new ArgumentNullException("baseConfiguration", "When creating a configuration from a Legacy.AccountServiceElement or Legacy.ActiveServiceElement, baseConfiguration must be supplied.");
				if (profile == null)
					throw new ArgumentNullException("profile", "When creating a configuration from a Legacy.AccountServiceElement or Legacy.ActiveServiceElement, profile must be supplied.");
			}
			if (legacyConfiguration is ServiceElement && !(legacyConfiguration is ActiveServiceElement))
			{
				if (baseConfiguration != null)
					throw new ArgumentException("baseConfiguration", "When creating a configuration from a Legacy.ServiceInstance, baseConfiguration must be null.");
				if (profile != null)
					throw new ArgumentException("profile", "When creating a configuration from a Legacy.ServiceInstance, profile must be null.");
			}

			ServiceElement legacy;
			if (legacyConfiguration is AccountServiceElement)
				legacy = new ActiveServiceElement(legacyConfiguration as AccountServiceElement);
			else if (legacyConfiguration is WorkflowStepElement)
				legacy = new ActiveServiceElement(legacyConfiguration as WorkflowStepElement);
			else
			{
				if (options != null)
				{
					legacy = new ActiveServiceElement((ServiceElement)legacyConfiguration);
					legacy.Options.Merge(options);
				}
				else
				{
					legacy = (ServiceElement)legacyConfiguration;
				}
			}
			
			T serviceConfiguration = new T()
			{
				Name = legacy.Name,
				MaxConcurrent = (legacy.MaxInstances == 0) ? 9999 : legacy.MaxInstances,
				MaxConcurrentPerProfile = (legacy.MaxInstancesPerAccount == 0) ? 9999 : legacy.MaxInstancesPerAccount,
				LegacyConfiguration = legacy,
				BaseConfiguration = baseConfiguration,
				SchedulingProfile = profile
			};

			if (legacy.Options.ContainsKey("ServicePriority"))
				serviceConfiguration.Priority = int.Parse(legacy.Options["ServicePriority"]);

			//scheduling rules 
			foreach (SchedulingRuleElement schedulingRuleElement in legacy.SchedulingRules)
			{
				SchedulingRule rule = SchedulingRule.FromLegacyRule(schedulingRuleElement);
				if (serviceConfiguration.SchedulingRules == null)
					serviceConfiguration.SchedulingRules = new List<SchedulingRule>();
				serviceConfiguration.SchedulingRules.Add(rule);
			}

			return serviceConfiguration;
		}
		
	}

	public class UnplannedServiceConfiguration: ServiceConfiguration
	{
		public ServiceInstance UnplannedInstance;

		public static UnplannedServiceConfiguration FromLegacyConfiguration(Legacy.ServiceInstance legacyInstance, ServiceConfiguration baseConfiguration = null, Profile profile = null, Dictionary<string, string> options = null)
		{
			UnplannedServiceConfiguration configuration = ServiceConfiguration.FromLegacyConfiguration<UnplannedServiceConfiguration>(
				legacyInstance.Configuration,
				baseConfiguration,
				profile,
				options
				);
			configuration.SchedulingRules.Clear();			
			return configuration;
		}
	}

}
