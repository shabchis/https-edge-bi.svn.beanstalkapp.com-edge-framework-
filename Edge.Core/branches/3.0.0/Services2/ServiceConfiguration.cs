using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Edge.Core.Services2.Scheduling;
using System.Diagnostics;

namespace Edge.Core.Services2
{
	[Serializable]
	public class ServiceConfiguration : ILockable, ISerializable
	{
		#region Fields
		//=================

		bool _isEnabled;
		bool _isPublic;
		string _assemblyPath;
		string _serviceType;
		string _serviceName;
		ServicePriority _priority;
		
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
			config.ParentConfiguration = this;
			config._isEnabled = true;
			config._isPublic = this._isPublic;
			config._assemblyPath = this._assemblyPath;
			config._serviceType = this._serviceType;
			config._serviceName = this._serviceName;
			config._priority = this._priority;
			this.Limits.CopyTo(config.Limits);
			
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

			// TODO: handle scheduling rules

			// Parameter inheritance from parent service configuration
			ServiceConfiguration parentInstanceConfig = parent as ServiceConfiguration;
			if (parentInstanceConfig != null && parentInstanceConfig.ConfigurationLevel == ServiceConfigurationLevel.Instance)
			{
				// Ignore parameters that are already in the config
				foreach (var param in parentInstanceConfig.Parameters)
					if (!config.Parameters.ContainsKey(param.Key))
						config.Parameters[param.Key] = param.Value;
			}

			return config;
		}

		//=================
		#endregion

		#region Properties
		//=================

		public Guid ConfigurationID
		{
			get;
			private set;
		}

		public ServiceConfigurationLevel ConfigurationLevel
		{
			get;
			internal set;
		}

		public ServiceConfiguration ParentConfiguration
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

		[DebuggerNonUserCode]
		public string AssemblyPath
		{
			get { return _assemblyPath; }
			set { _lock.Ensure(); _assemblyPath = value; }
		}

		[DebuggerNonUserCode]
		public string ServiceType
		{
			get { return _serviceType; }
			set { _lock.Ensure(); _serviceType = value; }
		}

		[DebuggerNonUserCode]
		public string ServiceName
		{
			get { return _serviceName; }
			set { _lock.Ensure(); _serviceName = value; }
		}

		[DebuggerNonUserCode]
		public bool IsEnabled
		{
			get { return _isEnabled; }
			set { _lock.Ensure(); _isEnabled = value; }
		}

		[DebuggerNonUserCode]
		public bool IsPublic
		{
			get { return _isPublic; }
			set { _lock.Ensure(); _isPublic = value; }
		}

		[DebuggerNonUserCode]
		public ServicePriority Priority
		{
			get { return _priority; }
			set { _lock.Ensure(); _priority = value; }
		}

		//=================
		#endregion

		#region Methods
		//=================

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

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			throw new NotImplementedException();
		}

		private ServiceConfiguration(SerializationInfo info, StreamingContext context)
		{
			throw new NotImplementedException();
		}
		
		//=================
		#endregion

		#region ILockable Members
		//=================

		#region Locking

		[NonSerialized] Padlock _lock = new Padlock();
		public bool IsLocked { get { return _lock.IsLocked; } }
		[DebuggerNonUserCode] void ILockable.Lock(object key)
		{
			_lock.Lock(key);
			((ILockable)this.Parameters).Lock(key);
			((ILockable)this.Limits).Lock(key);
			((ILockable)this.SchedulingRules).Lock(key);
		}
		[DebuggerNonUserCode] void ILockable.Unlock(object key)
		{
			_lock.Unlock(key);
			((ILockable)this.Parameters).Unlock(key);
			((ILockable)this.Limits).Unlock(key);
			((ILockable)this.SchedulingRules).Unlock(key);
		}

		#endregion



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
	public class ServiceExecutionLimits:ILockable
	{
		int _maxConcurrentGlobal = 0;
		int _maxConcurrentPerProfile = 0;
		int _maxConcurrentPerHost = 0;

		public int MaxConcurrentGlobal { get { return _maxConcurrentGlobal; } set { _lock.Ensure(); _maxConcurrentGlobal = value; } }
		public int MaxConcurrentPerProfile { get { return _maxConcurrentPerProfile; } set { _lock.Ensure(); _maxConcurrentPerProfile = value; } }
		public int MaxConcurrentPerHost { get { return _maxConcurrentPerHost; } set { _lock.Ensure(); _maxConcurrentPerHost = value; } }

		public void CopyTo(ServiceExecutionLimits serviceExecutionLimits)
		{
			serviceExecutionLimits.MaxConcurrentGlobal = this._maxConcurrentGlobal;
			serviceExecutionLimits.MaxConcurrentPerProfile = this._maxConcurrentGlobal;
			serviceExecutionLimits.MaxConcurrentPerHost = this._maxConcurrentGlobal;
		}

		#region Locking

		[NonSerialized] Padlock _lock = new Padlock();
		public bool IsLocked { get { return _lock.IsLocked; } }
		[DebuggerNonUserCode] void ILockable.Lock(object key) { _lock.Lock(key); }
		[DebuggerNonUserCode] void ILockable.Unlock(object key) { _lock.Unlock(key); }

		#endregion
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
