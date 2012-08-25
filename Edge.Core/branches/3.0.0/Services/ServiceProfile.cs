using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics;

namespace Edge.Core.Services
{
	[Serializable]
	public class ServiceProfile: Lockable, ISerializable
	{
		string _name;

		public Guid ProfileID { get; internal set; }
		public string Name { get { return _name; } set { EnsureUnlocked(); _name = value; } }
		public IDictionary<string, object> Parameters { get; private set; }
		public IList<ServiceConfiguration> Services { get; private set;}

		public ServiceProfile()
		{
			this.ProfileID = Guid.NewGuid();
			Parameters = new LockableDictionary<string, object>();
			Services = new LockableList<ServiceConfiguration>() { OnValidate = OnServiceAssigned };
		}

		bool OnServiceAssigned(int index, ServiceConfiguration item)
		{
			if (item.Profile != this)
				throw new InvalidOperationException("The profile being added is not associated with this profile. Use configuration.DeriveForProfile() to get a service configuration compatible with a profile.");
			return true;
		}

		public ConfigurationT NewConfiguration<ConfigurationT>() where ConfigurationT: ServiceConfiguration, new()
		{
			ConfigurationT config = new ConfigurationT();
			config.Profile = this;
			config.ConfigurationLevel = ServiceConfigurationLevel.Profile;

			return config;
		}

		public ServiceConfiguration DeriveConfiguration(ServiceConfiguration configuration)
		{
			if (configuration.ConfigurationLevel == ServiceConfigurationLevel.Instance)
				throw new ServiceConfigurationException("Cannot derive from a service instance configuration.");
			if (configuration.ConfigurationLevel == ServiceConfigurationLevel.Profile && configuration.Profile != this)
				throw new ServiceConfigurationException("Cannot derive from the configuration because it is associated with a different profile. Derive from configuration.TemplateConfiguration instead.");

			return configuration.Derive(this);
		}

		#region Locking
		//=================
		protected override IEnumerable<ILockable> GetLockables()
		{
			yield return (ILockable) Parameters;
			yield return (ILockable) Services;
		}

		//=================
		#endregion

		#region Serialization
		//=================

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("ID", ProfileID);
			info.AddValue("Name", Name);
			info.AddValue("Parameters", Parameters);

			info.AddValue("IsLocked", this.IsLocked);
		}

		private ServiceProfile(SerializationInfo info, StreamingContext context)
		{
			this.ProfileID = (Guid)info.GetValue("ID", typeof(Guid));
			this.Name = info.GetString("Name");
			this.Parameters = (IDictionary<string, object>)info.GetValue("Parameters", typeof(IDictionary<string, object>));

			// Was locked before serialization? Lock 'em up and throw away the key!
			if (info.GetBoolean("IsLocked"))
				((ILockable)this).Lock();
		}

		//=================
		#endregion
	}

}
