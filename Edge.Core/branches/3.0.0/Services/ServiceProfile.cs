using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics;

namespace Edge.Core.Services
{
	[Serializable]
	public class ServiceProfile: ILockable, ISerializable
	{
		string _name;

		public Guid ID { get; private set; }
		public string Name { get { return _name; } set { _lock.Ensure(); _name = value; } }
		public IDictionary<string, object> Parameters { get; private set; }
		public IList<ServiceConfiguration> AssignedServices { get; private set;}

		public ServiceProfile()
		{
			Parameters = new LockableDictionary<string, object>();
			AssignedServices = new LockableList<ServiceConfiguration>() { OnValidate = OnServiceAssigned };
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

		[NonSerialized] Padlock _lock = new Padlock();
		public bool IsLocked { get { return _lock.IsLocked; } }
		[DebuggerNonUserCode] void ILockable.Lock(object key)
		{
			_lock.Lock(key);
			((ILockable)Parameters).Lock(key);
			((ILockable)AssignedServices).Lock(key);
		}
		[DebuggerNonUserCode] void ILockable.Unlock(object key)
		{
			_lock.Unlock(key);
			((ILockable)Parameters).Lock(key);
			((ILockable)AssignedServices).Lock(key);
		}

		//=================
		#endregion

		#region Serialization
		//=================

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("ID", ID);
			info.AddValue("Name", Name);
			info.AddValue("Parameters", Parameters);

			info.AddValue("IsLocked", this.IsLocked);
		}

		private ServiceProfile(SerializationInfo info, StreamingContext context)
		{
			this.ID = (Guid)info.GetValue("ID", typeof(Guid));
			this.Name = info.GetString("Name");
			this.Parameters = (IDictionary<string, object>)info.GetValue("Parameters", typeof(IDictionary<string, object>));

			// Was locked before serialization? Lock 'em up and throw away the key!
			if (info.GetBoolean("IsLocked"))
				_lock.Lock(new object());
		}

		//=================
		#endregion
	}

}
