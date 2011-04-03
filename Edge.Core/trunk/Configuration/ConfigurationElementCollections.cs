using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Xml;

namespace Edge.Core.Configuration
{
	public abstract class ConfigurationElementCollectionBase<ElementT>: ConfigurationElementCollection, ISerializableConfigurationElement
		where ElementT: ConfigurationElement
	{
		public ElementT this[int index]
		{
			get
			{
				return (ElementT) base.BaseGet(index);
			}
			set
			{
				if (base.BaseGet(index) != null)
				{
					base.BaseRemoveAt(index);
				}
				base.BaseAdd(index, value);
			}
		}

		public int IndexOf(ConfigurationElement element)
		{
			for (int i = 0; i < this.Count; i++)
				if (this[i] == element)
					return i;

			return -1;
		}

		protected override ConfigurationPropertyCollection Properties
		{
			get
			{
				return new ConfigurationPropertyCollection();
			}
		}

		void ISerializableConfigurationElement.Deserialize(XmlReader reader)
		{
			this.DeserializeElement(reader, false);
		}
		void ISerializableConfigurationElement.Serialize(XmlWriter writer, string elementName)
		{
			this.SerializeToXmlElement(writer, elementName);
		}

		void ISerializableConfigurationElement.ResolveReferences(ServiceElementCollection services, ServiceElement service)
		{
			for (int i = 0; i < this.Count; i++)
			{
				if (this[i] is ISerializableConfigurationElement)
					(this[i] as ISerializableConfigurationElement).ResolveReferences(services, service);
			}
		}
	}

    /// <summary>
    /// Represents a collection of service elements.
    /// </summary>
	public class ServiceElementCollection: ConfigurationElementCollectionBase<ServiceElement>
    {
        #region Constructor
        public ServiceElementCollection()
        {
        }
        #endregion

        #region Properties
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }
        
        protected override string ElementName
        {
            get
            {
                return "Service";
            }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return new ConfigurationPropertyCollection();
            }
        }
        #endregion

        #region Indexers
        public new ServiceElement this[string name]
        {
            get
            {
                return (ServiceElement)base.BaseGet(name);
            }
        }
        #endregion

        #region Methods

		public ServiceElement GetService(string serviceName)
		{
			return base.BaseGet((object)serviceName) as ServiceElement;
		}


        public void Add(ServiceElement item)
        {
            base.BaseAdd(item);
        }

        public void Remove(ServiceElement item)
        {
            base.BaseRemove(item);
        }

        public void RemoveAt(int index)
        {
            base.BaseRemoveAt(index);
        }
        #endregion

        #region Overrides
        protected override ConfigurationElement CreateNewElement()
        {
            return new ServiceElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as ServiceElement).Name;
        }
        #endregion
    }

    /// <summary>
    /// Represents a collection of execution step elements.
    /// </summary>
	public class ExecutionStepElementCollection: ConfigurationElementCollectionBase<ExecutionStepElement>
    {
        #region Constructor
        public ExecutionStepElementCollection()
        {
        }
        #endregion

        #region Properties
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }

        protected override string ElementName
        {
            get
            {
                return "Step";
            }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return new ConfigurationPropertyCollection();
            }
        }
        #endregion

        #region Indexers
        public new ExecutionStepElement this[string name]
        {
            get
            {
                return (ExecutionStepElement)base.BaseGet(name);
            }
        }
        #endregion

        #region Methods
        public void Add(ExecutionStepElement item)
        {
            base.BaseAdd(item);
        }

        public void Remove(ExecutionStepElement item)
        {
            base.BaseRemove(item);
        }

        public void RemoveAt(int index)
        {
            base.BaseRemoveAt(index);
        }
        #endregion

        #region Overrides
        protected override ConfigurationElement CreateNewElement()
        {
            return new ExecutionStepElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
			// Get index name
			ExecutionStepElement step = (ExecutionStepElement)element;

			string name = step.Name != null ?
				step.Name : 
				(step.ServiceToUse.Element != null ? step.ServiceToUse.Element.Name : null);
			
			if (name == null || name.Trim().Length < 1)
				return IndexOf(step).ToString();
			else
				return name;
        }

        #endregion
    }

	/// <summary>
	/// Represents a collection of extension elements.
	/// </summary>
	public class ExtensionElementCollection: ConfigurationElementCollectionBase<ExecutionStepElement>
	{
		#region Constructor
		public ExtensionElementCollection()
		{
			if (ServicesConfiguration.IsLoading)
				ServicesConfiguration.Extensions = this;
		}
		#endregion

		#region Properties
		public override ConfigurationElementCollectionType CollectionType
		{
			get
			{
				return ConfigurationElementCollectionType.BasicMap;
			}
		}

		protected override string ElementName
		{
			get
			{
				return "Extension";
			}
		}

		protected override ConfigurationPropertyCollection Properties
		{
			get
			{
				return new ConfigurationPropertyCollection();
			}
		}
		#endregion

		#region Indexers
		public new ExtensionElement this[string name]
		{
			get
			{
				return (ExtensionElement) base.BaseGet(name);
			}
		}
		#endregion

		#region Methods
		public void Add(ExtensionElement item)
		{
			base.BaseAdd(item);
		}

		public void Remove(ExtensionElement item)
		{
			base.BaseRemove(item);
		}

		public void RemoveAt(int index)
		{
			base.BaseRemoveAt(index);
		}
		#endregion

		#region Overrides
		protected override ConfigurationElement CreateNewElement()
		{
			return new ExtensionElement();
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			// Get index name
			ExtensionElement extension = (ExtensionElement) element;
			return extension.Name;
		}

		#endregion
	}

	/// <summary>
    /// Represents a collection of scheduling rule elements.
    /// </summary>
	public class SchedulingRuleElementCollection: ConfigurationElementCollectionBase<SchedulingRuleElement>
    {
		#region Members
		private static ConfigurationPropertyCollection s_properties;
		private static ConfigurationProperty s_overrides;
		#endregion

		#region Constructor
		public SchedulingRuleElementCollection()
        {
			s_overrides = new ConfigurationProperty(
				"Overrides",
				typeof(bool),
				true);

			s_properties = new ConfigurationPropertyCollection();
			s_properties.Add(s_overrides);
        }
        #endregion

        #region Properties
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }

        protected override string ElementName
        {
            get
            {
                return "Rule";
            }
        }

		public bool Overrides
		{
			get
			{
				return (bool) base[s_overrides];
			}
			set
			{
				base[s_overrides] = value;
			}
		}
		
		protected override ConfigurationPropertyCollection Properties
        {
            get
            {
				return s_properties;
            }
        }
        #endregion

        #region Methods
        public void Add(SchedulingRuleElement item)
        {
            base.BaseAdd(item);
        }

        public void Remove(SchedulingRuleElement item)
        {
            base.BaseRemove(item);
        }

        public void RemoveAt(int index)
        {
            base.BaseRemoveAt(index);
        }
        #endregion

        #region Overrides
        protected override ConfigurationElement CreateNewElement()
        {
            return new SchedulingRuleElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return IndexOf(element);
        }

        #endregion
    }

    /// <summary>
    /// Represents a collection of account elements.
    /// </summary>
	public class AccountElementCollection: ConfigurationElementCollectionBase<AccountElement>
    {
        #region Constructor
        public AccountElementCollection()
        {
        }
        #endregion

        #region Properties
        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }
        
        protected override string ElementName
        {
            get
            {
                return "Account";
            }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return new ConfigurationPropertyCollection();
            }
        }

		public AccountElement GetAccount(int accountID)
		{
			return base.BaseGet((object) accountID) as AccountElement;
		}

        #endregion

        #region Methods
        public void Add(AccountElement item)
        {
            base.BaseAdd(item);
        }

        public void Remove(AccountElement item)
        {
            base.BaseRemove(item);
        }

        public void RemoveAt(int index)
        {
            base.BaseRemoveAt(index);
        }
        #endregion

        #region Overrides
        protected override ConfigurationElement CreateNewElement()
        {
            return new AccountElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return (element as AccountElement).ID;
        }
        #endregion
    }

	/// <summary>
	/// Represents a collection of services in an account.
	/// </summary>
	public class AccountServiceElementCollection: ConfigurationElementCollectionBase<AccountServiceElement>
	{
		#region Constructor
		public AccountServiceElementCollection()
		{
		}
		#endregion

		#region Properties
		public override ConfigurationElementCollectionType CollectionType
		{
			get
			{
				return ConfigurationElementCollectionType.BasicMap;
			}
		}

		protected override string ElementName
		{
			get
			{
				return "Service";
			}
		}

		protected override ConfigurationPropertyCollection Properties
		{
			get
			{
				return new ConfigurationPropertyCollection();
			}
		}

		public new AccountServiceElement this[string serviceName]
		{
			get
			{
				foreach (AccountServiceElement element in this)
				{
					if (element.Uses.Element.Name == serviceName)
						return element;
				}
				return null;
			}
		}

		#endregion

		#region Methods
		public void Add(AccountServiceElement item)
		{
			base.BaseAdd(item);
		}

		public void Remove(AccountServiceElement item)
		{
			base.BaseRemove(item);
		}

		public void RemoveAt(int index)
		{
			base.BaseRemoveAt(index);
		}

		#endregion

		#region Overrides
		protected override ConfigurationElement CreateNewElement()
		{
			return new AccountServiceElement();
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			return IndexOf(element);
		}
		#endregion
	}

	/// <summary>
    /// Represents a collection of service settings elements.
    /// </summary>
	public class AccountServiceSettingsElementCollection: ConfigurationElementCollectionBase<AccountServiceSettingsElement>
    {
        #region Constructor
		public AccountServiceSettingsElementCollection()
        {
        }
        #endregion

        #region Properties

		public AccountServiceSettingsElement this[ExecutionStepElement step]
		{
			get
			{
				foreach (AccountServiceSettingsElement elem in this)
				{
					if (elem.Step.Element == step)
						return elem;
				}

				return null;
			}
		}

        public override ConfigurationElementCollectionType CollectionType
        {
            get
            {
                return ConfigurationElementCollectionType.BasicMap;
            }
        }
        
        protected override string ElementName
        {
            get
            {
                return "ApplyTo";
            }
        }

        protected override ConfigurationPropertyCollection Properties
        {
            get
            {
                return new ConfigurationPropertyCollection();
            }
        }
        #endregion

        #region Methods
        public void Add(AccountServiceSettingsElement item)
        {
            base.BaseAdd(item);
        }

        public void Remove(AccountServiceSettingsElement item)
        {
            base.BaseRemove(item);
        }

        public void RemoveAt(int index)
        {
            base.BaseRemoveAt(index);
        }
        #endregion

        #region Overrides
        protected override ConfigurationElement CreateNewElement()
        {
            return new AccountServiceSettingsElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return IndexOf(element);
        }
        #endregion
    }
}
