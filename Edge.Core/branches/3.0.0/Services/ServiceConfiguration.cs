using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics;

namespace Edge.Core.Services
{
	[Serializable]
	public class ServiceConfiguration : Lockable, ISerializable
	{
		#region Fields
		//=================

		bool _isEnabled;
		bool _isPublic;
		string _assemblyPath;
		string _serviceType;
		string _serviceName;
		string _hostName;
		
		//=================
		#endregion
		
		#region Properties
		//=================

		public Guid ConfigurationID
		{
			get;

			// TEMP [Obsolete("This will be private soon so don't use it unless absolutely necessary.")]
			set;
		}

		public ServiceConfigurationLevel ConfigurationLevel
		{
			get;
			internal set;
		}

		public ServiceConfiguration BaseConfiguration
		{
			get;
			private set;
		}

		public ServiceProfile Profile
		{
			get;
			internal set;
		}

		public ServiceExecutionLimits Limits
		{
			get;
			private set;
		}

		public IDictionary<string, object> Parameters
		{
			get;
			private set;
		}

		public IList<SchedulingRule> SchedulingRules
		{
			get;
			private set;
		}

		public string AssemblyPath
		{
			get { return _assemblyPath; }
			set { EnsureUnlocked(); _assemblyPath = value; }
		}

		public string ServiceClass
		{
			get { return _serviceType; }
			set { EnsureUnlocked(); _serviceType = value; }
		}

		public string ServiceName
		{
			get { return _serviceName; }
			set { EnsureUnlocked(); _serviceName = value; }
		}

		public string HostName
		{
			get { return _hostName; }
			set { EnsureUnlocked(); _hostName = value; }
		}

		public bool IsEnabled
		{
			get { return _isEnabled; }
			set { EnsureUnlocked(); _isEnabled = value; }
		}

		public bool IsPublic
		{
			get { return _isPublic; }
			set { EnsureUnlocked(); _isPublic = value; }
		}

		//=================
		#endregion

		#region Constructors
		//=================
		// Not only constructors, but methods used to create configuration objects

		public ServiceConfiguration()
		{
			this.ConfigurationID = Guid.NewGuid();
			this.Limits = new ServiceExecutionLimits();
			this.ConfigurationLevel = ServiceConfigurationLevel.Template;
			this.Parameters = new LockableDictionary<string, object>();
			this.SchedulingRules = new LockableList<SchedulingRule>();
		}

		public ServiceConfiguration Derive()
		{
			return this.Derive(null);
		}

		internal ServiceConfiguration Derive(object parent)
		{
			if (this.ConfigurationLevel == ServiceConfigurationLevel.Instance)
				throw new ServiceConfigurationException("Cannot derive from an instance configuration.");

			ServiceConfiguration config;
			try { config = (ServiceConfiguration)Activator.CreateInstance(this.GetType()); }
			catch (MissingMethodException) { throw new MissingMethodException("Sub-types of ServiceConfiguration require a parameterless constructor."); }

			// Inherit from parent
			config.BaseConfiguration = this;
			config._isEnabled = true;
			config._isPublic = this._isPublic;
			config._assemblyPath = this._assemblyPath;
			config._serviceType = this._serviceType;
			config._serviceName = this._serviceName;
			this.Limits.CopyTo(config.Limits);

			OnDerive(config);
			
			// Merge parameters
			foreach (var param in this.Parameters)
				config.Parameters[param.Key] = param.Value;

			// Associate with profile
			ServiceProfile profile = parent as ServiceProfile;
			if (this.Profile != null)
			{
				if (profile == null)
				{
					profile = this.Profile;
				}
				else if (profile != this.Profile)
				{
					// This should never happen because it is checked by ServiceProfile.DeriveConfiguration()
					throw new ServiceConfigurationException("Profile mismatch - internal Derive should not be called this way");
				}
			}

			if (profile != null)
			{
				config.Profile = profile;
				config.ConfigurationLevel = ServiceConfigurationLevel.Profile;

				// Deriving from a new profile, so merge parameters (if this.Profile is not null, parameters were already merged in a previous Derive())
				if (this.Profile == null)
				{
					foreach (var param in profile.Parameters)
						config.Parameters[param.Key] = param.Value;
				}
			}

			// Get scheduling rules only if this one is empty
			foreach (SchedulingRule rule in this.SchedulingRules)
				config.SchedulingRules.Add(rule.Clone());

			// Parameter inheritance from parent service configuration
			ServiceConfiguration parentInstanceConfig = parent as ServiceConfiguration;
			if (parentInstanceConfig != null)
			{
				if (parentInstanceConfig.ConfigurationLevel == ServiceConfigurationLevel.Instance)
				{
					// Ignore parameters that are already in the config
					foreach (var param in parentInstanceConfig.Parameters)
						if (!config.Parameters.ContainsKey(param.Key))
							config.Parameters[param.Key] = param.Value;
				}
			}

			return config;
		}

		protected virtual void OnDerive(ServiceConfiguration newConfig)
		{
		}

		//=================
		#endregion

		#region Methods
		//=================

		public override bool Equals(object obj)
		{
			return obj is ServiceConfiguration?
				((ServiceConfiguration)obj).ConfigurationID == this.ConfigurationID :
				base.Equals(obj);
		}

		public static bool operator ==(ServiceConfiguration config1, ServiceConfiguration config2)
		{
			return Object.Equals(config1, config2);
		}

		public static bool operator !=(ServiceConfiguration config1, ServiceConfiguration config2)
		{
			return !Object.Equals(config1, config2);
		}

		/// <summary>
		/// Throws an exception if the current level is not the specified level.
		/// </summary>
		[DebuggerNonUserCode]
		private void EnsureLevel(ServiceConfigurationLevel level)
		{
			if (this.ConfigurationLevel != level)
				throw new InvalidOperationException("Cannot modify this property");
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="level">Indicates the configuration level (template, profile, or instance) to find.</param>
		/// <param name="search">Indicates whether the lowest ancestor or the highest ancestor should be found of the given type. (Highest not yet implemented).</param>
		/// <returns></returns>
		public ServiceConfiguration GetBaseConfiguration(ServiceConfigurationLevel level, ServiceConfigurationLevelSearch search = ServiceConfigurationLevelSearch.Lowest)
		{
			// TODO: implement ByLevel with search = Highest
			if (search == ServiceConfigurationLevelSearch.Highest)
				throw new NotSupportedException("Only the lowest level can be found at the moment.");

			ServiceConfiguration target = null;

			switch (level)
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

				case ServiceConfigurationLevel.Template:
					target = this;
					while (target.ConfigurationLevel != ServiceConfigurationLevel.Template && target.BaseConfiguration != null)
						target = target.BaseConfiguration;
					break;
			}
			return target;
		}

		public ServiceConfiguration GetTemplateConfiguration()
		{
			return GetBaseConfiguration(ServiceConfigurationLevel.Template);
		}

		public ServiceConfiguration GetProfileConfiguration()
		{
			return GetBaseConfiguration(ServiceConfigurationLevel.Profile);
		}

		public ServiceExecutionStatistics GetStatistics(int _percentile)
		{
			throw new NotImplementedException();
		}

		//=================
		#endregion

		#region Serialization
		//=================

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("ConfigurationID", ConfigurationID);
			info.AddValue("ConfigurationLevel", ConfigurationLevel);
			info.AddValue("BaseConfiguration", BaseConfiguration);
			info.AddValue("Profile", Profile);
			info.AddValue("Limits", Limits);
			info.AddValue("Parameters", Parameters);
			info.AddValue("SchedulingRules", SchedulingRules);
			info.AddValue("_assemblyPath", _assemblyPath);
			info.AddValue("_serviceType", _serviceType);
			info.AddValue("_serviceName", _serviceName);
			info.AddValue("_isEnabled", _isEnabled);
			info.AddValue("_isPublic", _isPublic);
			info.AddValue("IsLocked", IsLocked);
			Serialize(info, context);
		}

		protected virtual void Serialize(SerializationInfo info, StreamingContext context)
		{
		}

		protected ServiceConfiguration(SerializationInfo info, StreamingContext context)
		{
			this.ConfigurationID = (Guid)info.GetValue("ConfigurationID", typeof(Guid));
			this.ConfigurationLevel = (ServiceConfigurationLevel)info.GetValue("ConfigurationLevel", typeof(ServiceConfigurationLevel));
			this.BaseConfiguration = (ServiceConfiguration)info.GetValue("BaseConfiguration", typeof(ServiceConfiguration));
			this.Profile = (ServiceProfile)info.GetValue("Profile", typeof(ServiceProfile));
			this.Limits = (ServiceExecutionLimits)info.GetValue("Limits", typeof(ServiceExecutionLimits));
			this.Parameters = (IDictionary<string, object>)info.GetValue("Parameters", typeof(IDictionary<string, object>));
			this.SchedulingRules = (IList<SchedulingRule>)info.GetValue("SchedulingRules", typeof(IList<SchedulingRule>));
			
			_assemblyPath = info.GetString("_assemblyPath");
			_serviceType = info.GetString("_serviceType");
			_serviceName = info.GetString("_serviceName");
			_isEnabled = info.GetBoolean("_isEnabled");
			_isPublic = info.GetBoolean("_isPublic");

			Deserialize(info, context);

			if (info.GetBoolean("IsLocked"))
			    ((ILockable)this).Lock();
		}
	
		protected virtual void Deserialize(SerializationInfo info, StreamingContext context)
		{
		}
		
		//=================
		#endregion

		#region Lockable Members
		//=================

		protected override IEnumerable<ILockable>  GetLockables()
		{
			yield return (ILockable) this.Parameters;
			yield return (ILockable) this.Limits;
			yield return (ILockable) this.SchedulingRules;
		}

		//=================
		#endregion
	}

	public enum ServiceConfigurationLevel
	{
		Template,
		Profile,
		Instance
	}

	public enum ServiceConfigurationLevelSearch
	{
		Highest,
		Lowest
	}


	[Serializable]
	public class ServiceExecutionLimits:Lockable
	{
		int _maxConcurrentGlobal = 0;
		int _maxConcurrentPerProfile = 0;
		int _maxConcurrentPerHost = 0;
		TimeSpan _maxExecutionTime;
		public int MaxConcurrentGlobal { get { return _maxConcurrentGlobal; } set { EnsureUnlocked(); _maxConcurrentGlobal = value; } }
		public int MaxConcurrentPerProfile { get { return _maxConcurrentPerProfile; } set { EnsureUnlocked(); _maxConcurrentPerProfile = value; } }
		public int MaxConcurrentPerHost { get { return _maxConcurrentPerHost; } set { EnsureUnlocked(); _maxConcurrentPerHost = value; } }
		public TimeSpan MaxExecutionTime { get { return _maxExecutionTime; } set { EnsureUnlocked(); _maxExecutionTime = value; } }
		public void CopyTo(ServiceExecutionLimits serviceExecutionLimits)
		{
			serviceExecutionLimits.MaxConcurrentGlobal = this._maxConcurrentGlobal;
			serviceExecutionLimits.MaxConcurrentPerProfile = this._maxConcurrentGlobal;
			serviceExecutionLimits.MaxConcurrentPerHost = this._maxConcurrentGlobal;
		}

		
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

	[Serializable]
	public class ServiceConfigurationException : Exception
	{
		public ServiceConfigurationException() { }
		public ServiceConfigurationException(string message) : base(message) { }
		public ServiceConfigurationException(string message, Exception inner) : base(message, inner) { }
		protected ServiceConfigurationException(
		  SerializationInfo info,
		  StreamingContext context)
			: base(info, context) { }
	}
}
