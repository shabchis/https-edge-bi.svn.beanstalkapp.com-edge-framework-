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
		private ServiceConfiguration _baseConfiguration;
		private string _name;
		private Profile _schedulingProfile;
		private TimeSpan _averageExecutionTime = TimeSpan.FromMinutes(30);
		private ServiceElement _legacyConfiguration;
		private int _priority;
		int _maxConcurrent = 1;
		int _maxCuncurrentPerProfile = 1;

        public ServiceConfiguration()
        {
			this.SchedulingRules = new List<SchedulingRule>();
        }

		public bool IsLocked
		{
			get;
			private set;
		}

		public void Lock()
		{
			this.IsLocked = true;
		}

		public void EnsureUnlocked()
		{
			if (this.IsLocked)
				throw new InvalidOperationException("Service configuration is locked.");
		}

        public override int GetHashCode()
        {
			throw new NotImplementedException("GetHashCode");
            //return _guid.GetHashCode();
        }

		public int MaxConcurrent
		{
			get { return _maxConcurrent; }
			set
			{
				EnsureUnlocked();
				if (value != 0)
					_maxConcurrent = value;
				else
					value = 999;
			}
		}

		public ServiceConfiguration BaseConfiguration
		{
			get { return _baseConfiguration; }
			set { EnsureUnlocked(); _baseConfiguration = value; }
		}

		public string Name
		{
			get { return _name; }
			set { EnsureUnlocked(); _name = value; }
		}

		public Profile Profile
		{
			get { return _schedulingProfile; }
			set { EnsureUnlocked(); _schedulingProfile = value; }
		}

		public TimeSpan AverageExecutionTime
		{
			get { return _averageExecutionTime; }
			set { EnsureUnlocked(); _averageExecutionTime = value; }
		}

		public ServiceElement LegacyConfiguration
		{
			get { return _legacyConfiguration; }
			set { EnsureUnlocked(); _legacyConfiguration = value; }
		}

		public int Priority
		{
			get { return _priority; }
			set { EnsureUnlocked(); _priority = value; }
		}

		public List<SchedulingRule> SchedulingRules
		{
			get;
			private set;
		}

		public int MaxConcurrentPerProfile
		{
			get { return _maxCuncurrentPerProfile; }
			set
			{
				EnsureUnlocked();
				if (value != 0)
					_maxCuncurrentPerProfile = value;
				else
					value = 999;
			}
		}

		public TimeSpan MaxExecutionTime
		{
			get { return this._legacyConfiguration != null ? this._legacyConfiguration.MaxExecutionTime : TimeSpan.FromMinutes(45); }
			set
			{
				EnsureUnlocked();
				if (this._legacyConfiguration == null)
					throw new InvalidOperationException("Cannot set max execution time because LegacyConfiguration is not set.");
				this._legacyConfiguration.MaxExecutionTime = value;
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
				_name = legacy.Name,
				MaxConcurrent = (legacy.MaxInstances == 0) ? 9999 : legacy.MaxInstances,
				MaxConcurrentPerProfile = (legacy.MaxInstancesPerAccount == 0) ? 9999 : legacy.MaxInstancesPerAccount,
				_legacyConfiguration = legacy,
				_baseConfiguration = baseConfiguration,
				_schedulingProfile = profile
			};

			if (legacy.Options.ContainsKey("ServicePriority"))
				serviceConfiguration._priority = int.Parse(legacy.Options["ServicePriority"]);

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

		public virtual ServiceConfiguration Clone(bool includeSchedulingRules = true)
		{
			var cloned = (ServiceConfiguration) this.MemberwiseClone();
			if (!includeSchedulingRules)
				cloned.SchedulingRules.Clear();
			return cloned;
		}
	}

	public class ServiceInstanceConfiguration: ServiceConfiguration
	{
		public ServiceInstance Instance;

		public override ServiceConfiguration Clone(bool includeSchedulingRules = true)
		{
			throw new InvalidOperationException("Cannot clone ServiceInstanceConfiguration.");
		}

		public static ServiceInstanceConfiguration FromLegacyConfiguration(Legacy.ServiceInstance legacyInstance, ServiceConfiguration baseConfiguration = null, Profile profile = null, Dictionary<string, string> options = null)
		{
			ServiceInstanceConfiguration configuration = ServiceConfiguration.FromLegacyConfiguration<ServiceInstanceConfiguration>(
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
