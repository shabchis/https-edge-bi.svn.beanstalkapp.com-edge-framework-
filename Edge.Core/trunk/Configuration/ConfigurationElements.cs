using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Reflection;
using System.ComponentModel;
using System.Xml;
using System.Runtime.Serialization;
using System.Net;
using System.IO;

namespace Edge.Core.Configuration
{
	/// <summary>
	/// Base class for elements that have properties referencing other elements within the services section.
	/// </summary>
	public abstract class ReferencingConfigurationElement: ConfigurationElement, ISerializableConfigurationElement
	{
		#region Fields
		protected internal ConfigurationPropertyCollection InnerProperties;
		#endregion

		#region Constructor

		public ReferencingConfigurationElement()
		{
			InnerProperties = new ConfigurationPropertyCollection();
		}
		#endregion

		#region Properties

		protected override ConfigurationPropertyCollection Properties
		{
			get
			{
				return InnerProperties;
			}
		}

		internal new object this[ConfigurationProperty property]
		{
			get
			{
				return base[property];
			}
			set
			{
				base[property] = value;
			}
		}



		#endregion

		#region Methods

		internal virtual void ResolveReferences(ServiceElementCollection services, ServiceElement service)
		{
			((ISerializableConfigurationElement)this).ResolveReferences(services, service);
		}

		void ISerializableConfigurationElement.ResolveReferences(ServiceElementCollection services, ServiceElement service)
		{
			foreach (ConfigurationProperty property in InnerProperties)
			{
				if (property.Type == typeof(ElementReference<ServiceElement>))
				{
					ElementReference<ServiceElement> er = (ElementReference<ServiceElement>) this[property];

					if (er.Value != null)
					{
						ServiceElement element = services[er.Value];
						if (element == null)
							throw new ConfigurationErrorsException(String.Format("'{0}' is not a valid service reference.", er.Value));

						er.Element = element;
					}
				}
				else if (property.Type == typeof(ElementReference<WorkflowStepElement>) && service != null)
				{
					ElementReference<WorkflowStepElement> er = (ElementReference<WorkflowStepElement>) this[property];

					if (er.Value != null)
					{
						WorkflowStepElement element = service.Workflow[er.Value];
						if (element == null)
							throw new ConfigurationErrorsException(String.Format("'{0}' is not a valid execution step reference.", er.Value));

						er.Element = element;
					}
				}
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

		#endregion
	}

	public abstract class EnabledConfigurationElement: ReferencingConfigurationElement
	{
		#region Fields
		private ConfigurationProperty s_isEnabled;
		//private ConfigurationProperty s_options;
		protected SettingsCollection _options = new SettingsCollection();
		private Dictionary<string, ConfigurationElement> _extendedElements = new Dictionary<string, ConfigurationElement>();

		#endregion

		#region Constructor

		public EnabledConfigurationElement()
		{
			s_isEnabled = new ConfigurationProperty(
				"IsEnabled",
				typeof(bool),
				true);

			//s_options = new ConfigurationProperty(
			//    "Options",
			//    typeof(SettingsCollection),
			//    null,
			//    Converters.SettingsCollectionConverter.Instance,
			//    null,
			//    ConfigurationPropertyOptions.None
			//    );

			InnerProperties.Add(s_isEnabled);
			//InnerProperties.Add(s_options);
		}
		#endregion

		#region Properties
		public bool IsEnabled
		{
			get
			{
				return (bool) base[s_isEnabled];
			}
			set
			{
				base[s_isEnabled] = value;
			}
		}

		public SettingsCollection Options
		{
			get
			{
				//return (SettingsCollection)base[s_options];
				return _options;
			}
		}

		public Dictionary<string, ConfigurationElement> Extensions
		{
			get { return _extendedElements; }
			internal set { _extendedElements = value; }
		}

		#endregion

		#region Methods

		protected override bool OnDeserializeUnrecognizedAttribute(string name, string value)
		{
			if (Options.ContainsKey(name))
				//throw new ConfigurationErrorsException(String.Format("The option '{0}' is already defined either as a custom attribute or in the Options attribute.", name));
				throw new ConfigurationErrorsException(String.Format("The option '{0}' is already defined as a custom attribute", name));

			Options.Add(name, value);
			return true;
		}

		protected override bool OnDeserializeUnrecognizedElement(string elementName, XmlReader reader)
		{
			// Try to retrieve the extension type data
			Type t = EdgeServicesConfiguration.GetExtension(elementName);
			if (t == null)
				return false;

			if (this.Properties.Contains(elementName))
				throw new ConfigurationErrorsException(String.Format("The extension element '{0}' is defined more than once in this service.", elementName));

			// Check the type
			if (t == null)
				throw new ConfigurationErrorsException("The specified extension type could not be found.");
			if (t.GetInterface(typeof(ISerializableConfigurationElement).FullName) == null || !t.IsSubclassOf(typeof(ConfigurationElement)))
				throw new ConfigurationErrorsException("The specified extension type must inherit from ConfigurationElement and implement IDeserializableConfigurationElement.");

			// Check the constructor
			ConstructorInfo ctor = t.GetConstructor(Type.EmptyTypes);
			if (ctor == null)
				throw new ConfigurationErrorsException("The extension type must include a default parameterless constructor.");

			// Create the element
			ISerializableConfigurationElement elem = (ISerializableConfigurationElement)ctor.Invoke(new object[] { });
			elem.Deserialize(reader);

			// Register a new property for this element - for serialization
			ConfigurationProperty newProperty = new ConfigurationProperty(
				elementName,
				elem.GetType()
			);
			InnerProperties.Add(newProperty);

			// Add the element to the recognized elements
			_extendedElements.Add(elementName, (ConfigurationElement)elem);
			return true;
		}

		// Serialize extended elements - in theory should have happened automatically
		// because of the newProperty added to InnerProperties via OnDeserializeUnrecognizedElement,
		// but this is not the case
		protected override bool SerializeElement(XmlWriter writer, bool serializeCollectionKey)
		{
			// Writer is null before the start tag is written, and is a valid object
			//	once it is time to write content
			if (writer != null)
			{
				foreach (KeyValuePair<string, string> pair in Options)
					writer.WriteAttributeString(pair.Key, pair.Value);
			}

			bool wasSerialized = base.SerializeElement(writer, serializeCollectionKey);

			if (writer != null)
			{
				foreach (KeyValuePair<string, ConfigurationElement> pair in _extendedElements)
				{
					// Serialize with the element name
					ConfigurationElement elem = pair.Value;
					((ISerializableConfigurationElement)elem).Serialize(writer, pair.Key);
				}
			}

			return wasSerialized;
		}

		#endregion
	}

	public abstract class NamedConfigurationElement: EnabledConfigurationElement
	{
		#region Fields
		protected ConfigurationProperty s_name;
		#endregion

		#region Constructor

		public NamedConfigurationElement(bool nameRequired)
		{
			s_name = new ConfigurationProperty(
				"Name",
				typeof(string),
				null,
				 ConfigurationPropertyOptions.IsKey |
					(nameRequired ?
						ConfigurationPropertyOptions.IsRequired :
						ConfigurationPropertyOptions.None
					)
			);

			InnerProperties.Add(s_name);
		}
		#endregion

		#region Properties
		public string Name
		{
			get
			{
				return (string) base[s_name];
			}
			set
			{
				base[s_name] = value;
			}
		}

		#endregion

		#region Methods

		public override string ToString()
		{
			return String.Format("{0}: {1}", this.GetType().Name, this.Name);
		}

		#endregion
	}

    /// <summary>
    /// Represents a single service
    /// </summary>
	public class ServiceElement: NamedConfigurationElement
    {
		public static TimeSpan DefaultMaxExecutionTime = TimeSpan.FromMinutes(15);

        #region Fields

        private ConfigurationProperty s_isPublic;
		private ConfigurationProperty s_class;
		private ConfigurationProperty s_path;
        private ConfigurationProperty s_arguments;
        private ConfigurationProperty s_maxInstances;
        private ConfigurationProperty s_maxInstancesPerAccount;
        private ConfigurationProperty s_maxExecutionTime;
        private ConfigurationProperty s_serviceType;
        private ConfigurationProperty s_executionFlow;
        private ConfigurationProperty s_schedulingRules;
		private ConfigurationProperty s_debugDelay;
		
		#endregion

        #region Constructor
        public ServiceElement(): base(true)
        {
			s_isPublic = new ConfigurationProperty(
				"IsPublic",
				typeof(bool),
				true);

            s_path = new ConfigurationProperty(
				"ProcessPath",
                typeof(string));

            s_arguments = new ConfigurationProperty(
				"ProcessArguments",
                typeof(string));

            s_class = new ConfigurationProperty(
                "Class",
                typeof(string));

            s_maxInstances = new ConfigurationProperty(
                "MaxInstances",
                typeof(int),
                0);

            s_maxInstancesPerAccount = new ConfigurationProperty(
                "MaxInstancesPerAccount",
                typeof(int),
                0);

            s_maxExecutionTime = new ConfigurationProperty(
                "MaxExecutionTime",
                typeof(TimeSpan),
                ServiceElement.DefaultMaxExecutionTime);

            s_serviceType = new ConfigurationProperty(
                "ServiceType",
                typeof(ServiceType),
                ServiceType.Class);

            s_executionFlow = new ConfigurationProperty(
                "Workflow",
                typeof(WorkflowStepElementCollection),
                null,
				ConfigurationPropertyOptions.IsDefaultCollection);

            s_schedulingRules = new ConfigurationProperty(
                "SchedulingRules",
                typeof(SchedulingRuleElementCollection),
				null,
				ConfigurationPropertyOptions.IsDefaultCollection);

			s_debugDelay = new ConfigurationProperty(
				"DebugDelay",
				typeof(TimeSpan),
				TimeSpan.Zero);

			InnerProperties.Add(s_isPublic);
			InnerProperties.Add(s_path);
			InnerProperties.Add(s_arguments);
			InnerProperties.Add(s_class);
            InnerProperties.Add(s_maxInstances);
            InnerProperties.Add(s_maxInstancesPerAccount);
            InnerProperties.Add(s_maxExecutionTime);
            InnerProperties.Add(s_serviceType);
            InnerProperties.Add(s_executionFlow);
			InnerProperties.Add(s_schedulingRules);
			InnerProperties.Add(s_debugDelay);
        }
        #endregion

        #region Properties

		

		public bool IsPublic
		{
			get { return (bool)base[s_isPublic]; }
			set { base[s_isPublic] = value; }
		}

		public string ProcessPath
		{
			get { return (string) base[s_path]; }
			set { base[s_path] = value; }
		}

		public string ProcessArguments
		{
			get { return (string)base[s_arguments]; }
			set { base[s_arguments] = value; }
		}

		public string Class
        {
            get { return (string)base[s_class]; }
            set { base[s_class] = value; }
        }

       
		public int MaxInstances
        {
            get { return (int)base[s_maxInstances]; }
            set { base[s_maxInstances] = value; }
        }

        public int MaxInstancesPerAccount
        {
            get { return (int)base[s_maxInstancesPerAccount]; }
            set { base[s_maxInstancesPerAccount] = value; }
        }

        public TimeSpan MaxExecutionTime
        {
            get
            {
                return (TimeSpan)base[s_maxExecutionTime];
            }
            set
            {
                base[s_maxExecutionTime] = value;
            }
        }

        public ServiceType ServiceType
        {
            get { return (ServiceType)base[s_serviceType]; }
            set { base[s_serviceType] = value; }
        }

        public WorkflowStepElementCollection Workflow
        {
            get { return (WorkflowStepElementCollection)base[s_executionFlow]; }
            set { base[s_executionFlow] = value; }
        }

        public SchedulingRuleElementCollection SchedulingRules
        {
            get { return (SchedulingRuleElementCollection)base[s_schedulingRules]; }
            set { base[s_schedulingRules] = value; }
        }

		public TimeSpan DebugDelay
		{
			get { return (TimeSpan)base[s_debugDelay]; }
			set { base[s_debugDelay] = value; }
		}


		#endregion

		#region Internal Methods

		internal override void ResolveReferences(ServiceElementCollection services, ServiceElement service)
		{
			base.ResolveReferences(services, service);
			foreach(WorkflowStepElement step in this.Workflow)
				step.ResolveReferences(services, this);

			foreach (ConfigurationElement element in this.Extensions.Values)
			{
				if (element is ISerializableConfigurationElement)
					(element as ISerializableConfigurationElement).ResolveReferences(services, this);
			}
		}



		#endregion
	}

    /// <summary>
    /// Represents a single execution step element.
    /// </summary>
	public class WorkflowStepElement: NamedConfigurationElement
    {
		#region Members
        
        private ConfigurationProperty s_serviceToUse;
        private ConfigurationProperty s_maxExecutionTime;
        private ConfigurationProperty s_waitForPrevious;
        private ConfigurationProperty s_blocking;
        private ConfigurationProperty s_failureRepeat;
        private ConfigurationProperty s_repeatDelayMin;
        private ConfigurationProperty s_repeatDelayMax;
        private ConfigurationProperty s_failureOutcome;
        private ConfigurationProperty s_failureHandler;
		private ConfigurationProperty s_isFailureHandler;
		private ConfigurationProperty s_condition;
		private ConfigurationProperty s_conditionOptions;

        #endregion

        #region Constructor
        public WorkflowStepElement(): base(false)
        {
            s_serviceToUse = new ConfigurationProperty(
                "Base",
                typeof(ElementReference<ServiceElement>),
				new ElementReference<ServiceElement>(),
				new Converters.ElementReferenceConverter<ServiceElement>(this, "Base"),
				null,
                ConfigurationPropertyOptions.None);

            s_maxExecutionTime = new ConfigurationProperty(
                "MaxExecutionTime",
                typeof(TimeSpan),
				ServiceElement.DefaultMaxExecutionTime
				);

            s_waitForPrevious = new ConfigurationProperty(
                "WaitForPrevious",
                typeof(bool),
                false);

            s_blocking = new ConfigurationProperty(
                "IsBlocking",
                typeof(bool),
                true);

            s_failureRepeat = new ConfigurationProperty(
                "FailureRepeat",
                typeof(int),
                0);

            s_repeatDelayMin = new ConfigurationProperty(
                "RepeatDelayMin",
                typeof(TimeSpan),
                TimeSpan.FromMilliseconds(0));

            s_repeatDelayMax = new ConfigurationProperty(
                "RepeatDelayMax",
                typeof(TimeSpan),
				TimeSpan.FromMilliseconds(0));

            s_failureOutcome = new ConfigurationProperty(
                "FailureOutcome",
                typeof(FailureOutcome),
				FailureOutcome.Unspecified);

            s_failureHandler = new ConfigurationProperty(
                "FailureHandler",
                typeof(ElementReference<WorkflowStepElement>),
				new ElementReference<WorkflowStepElement>(),
				new Converters.ElementReferenceConverter<WorkflowStepElement>(this, "FailureHandler"),
				null,
				ConfigurationPropertyOptions.None
				);

            s_isFailureHandler = new ConfigurationProperty(
                "IsFailureHandler",
                typeof(bool),
                false);

			s_condition = new ConfigurationProperty(
				"Condition",
				typeof(string));

			s_conditionOptions = new ConfigurationProperty(
				"ConditionOptions",
				typeof(string[]),
				new string[0],
				Converters.StringArrayConverter.Instance,
				null, ConfigurationPropertyOptions.None
				);

            InnerProperties.Add(s_serviceToUse);
            InnerProperties.Add(s_maxExecutionTime);
            InnerProperties.Add(s_waitForPrevious);
            InnerProperties.Add(s_blocking);
            InnerProperties.Add(s_failureRepeat);
            InnerProperties.Add(s_repeatDelayMin);
            InnerProperties.Add(s_repeatDelayMax);
            InnerProperties.Add(s_failureOutcome);
            InnerProperties.Add(s_failureHandler);
			InnerProperties.Add(s_isFailureHandler);
			InnerProperties.Add(s_condition);
			InnerProperties.Add(s_conditionOptions);
        }
        #endregion

        #region Properties

		public ElementReference<ServiceElement> BaseConfiguration
        {
            get
            {
				return (ElementReference<ServiceElement>) base[s_serviceToUse];
            }
            set
            {
                base[s_serviceToUse] = value;
            }
        }


        public TimeSpan MaxExecutionTime
        {
            get
            {
                return (TimeSpan)base[s_maxExecutionTime];
            }
            set
            {
                base[s_maxExecutionTime] = value;
            }
        }

        public bool WaitForPrevious
        {
            get
            {
                return (bool)base[s_waitForPrevious];
            }
            set
            {
                base[s_waitForPrevious] = value;
            }
        }

        public bool IsBlocking
        {
            get
            {
                return (bool)base[s_blocking];
            }
            set
            {
                base[s_blocking] = value;
            }
        }

        public int FailureRepeat
        {
            get
            {
                return (int)base[s_failureRepeat];
            }
            set
            {
                base[s_failureRepeat] = value;
            }
        }

        public TimeSpan RepeatDelayMin
        {
            get
            {
                return (TimeSpan)base[s_repeatDelayMin];
            }
            set
            {
                base[s_repeatDelayMin] = value;
            }
        }

        public TimeSpan RepeatDelayMax
        {
            get
            {
                return (TimeSpan)base[s_repeatDelayMax];
            }
            set
            {
                base[s_repeatDelayMax] = value;
            }
        }

        public FailureOutcome FailureOutcome
        {
            get
            {
				FailureOutcome outcome = (FailureOutcome)base[s_failureOutcome];

				if (outcome == FailureOutcome.Unspecified)
					return FailureHandler.Element != null ? FailureOutcome.Handler : FailureOutcome.Terminate;
				else
					return outcome;
            }
            set
            {
                base[s_failureOutcome] = value;
            }
        }

		public ElementReference<WorkflowStepElement> FailureHandler
        {
            get
            {
				return (ElementReference<WorkflowStepElement>) base[s_failureHandler];
            }
            set
            {
                base[s_failureHandler] = value;
            }
        }

        public bool IsFailureHandler
        {
            get
            {
                return (bool)base[s_isFailureHandler];
            }
            set
            {
                base[s_isFailureHandler] = value;
            }
        }

		public string Condition
		{
			get
			{
				return (string) base[s_condition];
			}
			set
			{
				base[s_condition] = value;
			}
		}

		public string[] ConditionOptions
		{
			get
			{
				return (string[]) base[s_conditionOptions];
			}
			set
			{
				base[s_conditionOptions] = value;
			}
		}

		public string ActualName
		{
			get
			{
				return this.Name ??
				(
					this.BaseConfiguration == null ? "(empty)" :
					this.BaseConfiguration.Element == null ? "(empty)" :
					this.BaseConfiguration.Element.Name
				);
			}
		}

		#endregion
    }

    /// <summary>
    /// Represents a single scheduling rule element.
    /// </summary>
	[Serializable]
	public class SchedulingRuleElement: ReferencingConfigurationElement, ISerializable
    {
        #region Members
        private ConfigurationProperty s_calendarUnit;
        private ConfigurationProperty s_subUnits;
        private ConfigurationProperty s_exactTimes;
        private ConfigurationProperty s_frequency;
        private ConfigurationProperty s_maxDeviation;
		private ConfigurationProperty s_fullSchedule;
		private ConfigurationProperty s_fromDate;
		private ConfigurationProperty s_toDate;
		private ConfigurationProperty s_excatUnits;
		private ConfigurationProperty s_nextDay;
        #endregion

        #region Constructor
		public SchedulingRuleElement()
        {
			s_fullSchedule = new ConfigurationProperty(
			   "FullSchedule",
			   typeof(string),
			   null,
			   ConfigurationPropertyOptions.None);

			s_fromDate = new ConfigurationProperty(
			   "FromDate",
			   typeof(string),
			   null,
			   ConfigurationPropertyOptions.None);

			s_toDate = new ConfigurationProperty(
			   "ToDate",
			   typeof(string),
			   null,
			   ConfigurationPropertyOptions.None);

			s_excatUnits = new ConfigurationProperty(
			  "ExactUnits",
			   typeof(string),
			   null,
			   ConfigurationPropertyOptions.None);

            s_calendarUnit = new ConfigurationProperty(
                "CalendarUnit",
                typeof(CalendarUnit),
                null,
                ConfigurationPropertyOptions.IsRequired);

            s_subUnits = new ConfigurationProperty(
                "SubUnits",
                typeof(int[]),
				new int[0],
				Converters.Int32ArrayConverter.Instance,
				null, ConfigurationPropertyOptions.None
				);

            s_exactTimes = new ConfigurationProperty(
                "ExactTimes",
                typeof(TimeSpan[]),
				new TimeSpan[0],
				Converters.TimeSpanArrayConverter.Instance,
				null, ConfigurationPropertyOptions.None
				);

            s_frequency = new ConfigurationProperty(
                "Frequency",
                typeof(TimeSpan));

            s_maxDeviation = new ConfigurationProperty(
                "MaxDeviation",
                typeof(TimeSpan));

			s_nextDay = new ConfigurationProperty(
				"NextDay",
				typeof(bool),
				false,
				ConfigurationPropertyOptions.None);

			InnerProperties.Add(s_excatUnits);
			InnerProperties.Add(s_fromDate);
			InnerProperties.Add(s_toDate);
			InnerProperties.Add(s_fullSchedule);
            InnerProperties.Add(s_calendarUnit);
            InnerProperties.Add(s_subUnits);
            InnerProperties.Add(s_exactTimes);
            InnerProperties.Add(s_frequency);
            InnerProperties.Add(s_maxDeviation);
			InnerProperties.Add(s_nextDay);
        }
        #endregion

        #region Properties

		public bool NextDay
		{
			get
			{
				return (bool)base[s_nextDay];
			}
			set
			{
				base[s_nextDay] = value;
			}
		}

		public string FullSchedule
		{
			get
			{
				return (string)base[s_fullSchedule];
			}
			set
			{
				base[s_fullSchedule] = value;
			}
		}

		public string FromDate
		{
			get
			{
				return (string)base[s_fromDate];
			}
			set
			{
				base[s_fromDate] = value;
			}
		}

		public string ExcatUnits
		{
			get
			{
				return (string)base[s_excatUnits];
			}
			set
			{
				base[s_excatUnits] = value;
			}
		}

		public string ToDate
		{
			get
			{
				return (string)base[s_toDate];
			}
			set
			{
				base[s_toDate] = value;
			}
		}

        public CalendarUnit CalendarUnit
        {
            get
            {
                return (CalendarUnit)base[s_calendarUnit];
            }
            set
            {
                base[s_calendarUnit] = value;
            }
        }

        public int[] SubUnits
        {
            get
            {
                return (int[])base[s_subUnits];
            }
            set
            {
                base[s_subUnits] = value;
            }
        }

        public TimeSpan[] ExactTimes
        {
            get
            {
                return (TimeSpan[])base[s_exactTimes];
            }
            set
            {
                base[s_exactTimes] = value;
            }
        }

        public TimeSpan Frequency
        {
            get
            {
                return (TimeSpan)base[s_frequency];
            }
            set
            {
                base[s_frequency] = value;
            }
        }

        public TimeSpan MaxDeviation
        {
            get
            {
                return (TimeSpan)base[s_maxDeviation];
            }
            set
            {
                base[s_maxDeviation] = value;
            }
        }
        #endregion

		#region ISerializable Members

		private SchedulingRuleElement(SerializationInfo info, StreamingContext context)
		{
			StringReader stringReader = new StringReader(info.GetString("xml"));
			XmlTextReader reader = new XmlTextReader(stringReader);
			reader.ReadToFollowing("SchedulingRule");
			this.DeserializeElement(reader, false);
			this.ResolveReferences(EdgeServicesConfiguration.Current.Services, null);
		}

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("xml", GetXml());
		}

		public string GetXml()
		{
			StringWriter stringWriter = new StringWriter();
			XmlTextWriter writer = new XmlTextWriter(stringWriter);
			this.SerializeToXmlElement(writer, "SchedulingRule");
			return stringWriter.ToString();
		}

		#endregion
    }

    /// <summary>
    /// Represents a single account element.
    /// </summary>
	public class AccountElement: EnabledConfigurationElement
    {
        #region Members
        private ConfigurationProperty s_id;
        private ConfigurationProperty s_name;
        private ConfigurationProperty s_services;
        #endregion

        #region Constructor
		public AccountElement()
        {
            s_id = new ConfigurationProperty(
                "ID",
                typeof(int),
                null,
                ConfigurationPropertyOptions.IsRequired);

            s_name = new ConfigurationProperty(
                "Name",
                typeof(string),
                null,
                ConfigurationPropertyOptions.IsRequired);

            s_services = new ConfigurationProperty(
                "Services",
				typeof(AccountServiceElementCollection),
                null,
                ConfigurationPropertyOptions.IsRequired);

            InnerProperties.Add(s_id);
            InnerProperties.Add(s_name);
			InnerProperties.Add(s_services);
        }
        #endregion

        #region Properties
        public string Name
        {
            get
            {
                return (string)base[s_name];
            }
            set
            {
                base[s_name] = value;
            }
        }

        public int ID
        {
            get
            {
                return (int)base[s_id];
            }
            set
            {
                base[s_id] = value;
            }
        }

        public AccountServiceElementCollection Services
        {
            get
            {
				return (AccountServiceElementCollection) base[s_services];
            }
            set
            {
                base[s_services] = value;
            }
        }
        #endregion

		#region Internal methods

		internal override void ResolveReferences(ServiceElementCollection services, ServiceElement service)
		{
			base.ResolveReferences(services, service);
			foreach(AccountServiceElement acService in this.Services)
				acService.ResolveReferences(services, null);
		}

		//internal override void SuspendReferences()
		//{
		//    base.SuspendReferences();
		//    foreach (AccountServiceElement acService in this.Services)
		//        acService.SuspendReferences();
		//}

		//internal override void RestoreReferences()
		//{
		//    base.RestoreReferences();
		//    foreach (AccountServiceElement acService in this.Services)
		//        acService.RestoreReferences();
		//}

		#endregion

	}

	/// <summary>
	/// 
	/// </summary>
	public class AccountServiceElement: EnabledConfigurationElement
	{
		#region Members
		private ConfigurationProperty s_uses;
		private ConfigurationProperty s_settings;
		private ConfigurationProperty s_schedulingRules;
		#endregion

		#region Constructor
		public AccountServiceElement()
		{
			s_uses = new ConfigurationProperty(
				"Uses",
				typeof(ElementReference<ServiceElement>),
				new ElementReference<ServiceElement>(),
				new Converters.ElementReferenceConverter<ServiceElement>(this, "Uses"),
				null,
				ConfigurationPropertyOptions.IsRequired);

			s_settings = new ConfigurationProperty(
				"Settings",
				typeof(AccountServiceSettingsElementCollection),
				null,
				ConfigurationPropertyOptions.IsDefaultCollection);

			s_schedulingRules = new ConfigurationProperty(
				"SchedulingRules",
				typeof(SchedulingRuleElementCollection),
				null,
				ConfigurationPropertyOptions.IsDefaultCollection);

			InnerProperties.Add(s_uses);
			InnerProperties.Add(s_settings);
			InnerProperties.Add(s_schedulingRules);

		}
		#endregion

		#region Properties
		public SchedulingRuleElementCollection SchedulingRules
		{
			get
			{
				return (SchedulingRuleElementCollection) base[s_schedulingRules];
			}
			set
			{
				base[s_schedulingRules] = value;
			}
		}

		public ElementReference<ServiceElement> Uses
		{
			get
			{
				return (ElementReference<ServiceElement>) base[s_uses];
			}
			set
			{
				base[s_uses] = value;
			}
		}

		public AccountServiceSettingsElementCollection Settings
		{
			get
			{
				return (AccountServiceSettingsElementCollection) base[s_settings];
			}
			set
			{
				base[s_settings] = value;
			}
		}

		#endregion

		#region Internal methods

		internal override void ResolveReferences(ServiceElementCollection services, ServiceElement service)
		{
			base.ResolveReferences(services, service);
			foreach (AccountServiceSettingsElement setting in this.Settings)
				setting.ResolveReferences(services, this.Uses.Element);
		}

		#endregion
	}

	/// <summary>
	/// 
	/// </summary>
	public class AccountServiceSettingsElement: EnabledConfigurationElement
    {
        #region Members
        private ConfigurationProperty s_step;
        private ConfigurationProperty s_arguments;
        private ConfigurationProperty s_maxExecutionTime;
        private ConfigurationProperty s_failureRepeat;
        private ConfigurationProperty s_repeatDelayMin;
        private ConfigurationProperty s_repeatDelayMax;
        #endregion

        #region Constructor
		/*=========================*/
		public AccountServiceSettingsElement()
        {
            s_step = new ConfigurationProperty(
                "Step",
                typeof(ElementReference<WorkflowStepElement>),
				new ElementReference<WorkflowStepElement>(),
				new Converters.ElementReferenceConverter<WorkflowStepElement>(this, "Step"),
				null,
                ConfigurationPropertyOptions.IsRequired);

            s_arguments = new ConfigurationProperty(
                "Arguments",
                typeof(string),
                null);

            s_maxExecutionTime = new ConfigurationProperty(
                "MaxExecutionTime",
                typeof(TimeSpan),
                null);

            s_failureRepeat = new ConfigurationProperty(
                "FailureRepeat",
                typeof(int),
                null);

            s_repeatDelayMin = new ConfigurationProperty(
                "RepeatDelayMin",
                typeof(TimeSpan),
                null);

            s_repeatDelayMax = new ConfigurationProperty(
                "RepeatDelayMax",
                typeof(TimeSpan),
                null);

            InnerProperties.Add(s_step);
            InnerProperties.Add(s_arguments);
            InnerProperties.Add(s_maxExecutionTime);
            InnerProperties.Add(s_failureRepeat);
            InnerProperties.Add(s_repeatDelayMin);
            InnerProperties.Add(s_repeatDelayMax);
        }
        #endregion

        #region Properties
		public ElementReference<WorkflowStepElement> Step
        {
            get
            {
				return (ElementReference<WorkflowStepElement>) base[s_step];
            }
            set
            {
                base[s_step] = value;
            }
        }

        public string Arguments
        {
            get
            {
                return (string)base[s_arguments];
            }
            set
            {
                base[s_arguments] = value;
            }
        }

        public TimeSpan MaxExecutionTime
        {
            get
            {
                return (TimeSpan)base[s_maxExecutionTime];
            }
            set
            {
                base[s_maxExecutionTime] = value;
            }
        }

        public int FailureRepeat
        {
            get
            {
                return (int)base[s_failureRepeat];
            }
            set
            {
                base[s_failureRepeat] = value;
            }
        }

        public TimeSpan RepeatDelayMin
        {
            get
            {
                return (TimeSpan)base[s_repeatDelayMin];
            }
            set
            {
                base[s_repeatDelayMin] = value;
            }
        }

        public TimeSpan RepeatDelayMax
        {
            get
            {
                return (TimeSpan)base[s_repeatDelayMax];
            }
            set
            {
                base[s_repeatDelayMax] = value;
            }
        }

        #endregion

    }

	/// <summary>
	/// 
	/// </summary>
	[Serializable]
	public class ActiveServiceElement: ServiceElement, ISerializable
	{
		#region Members
		/*=========================*/
		AccountServiceSettingsElementCollection _stepSettings;

		/*=========================*/
		#endregion
		
		#region Constructors
		/*=========================*/
		
		public ActiveServiceElement(ServiceElement serviceElement)
		{
			if (serviceElement == null)
				throw new ArgumentNullException("serviceElement");

			// Apply base service properties
			foreach (ConfigurationProperty property in serviceElement.InnerProperties)
				ApplyProperty(serviceElement, property, true);

			// Apply options
			foreach (KeyValuePair<string, string> pair in serviceElement.Options)
				this.Options.Add(pair.Key, pair.Value);

			// Add extended elements
			foreach (KeyValuePair<string, ConfigurationElement> pair in serviceElement.Extensions)
				this.Extensions.Add(pair.Key, pair.Value);
			
			// Inherit step settings from parent step settings
			if (serviceElement is ActiveServiceElement)
				_stepSettings = ((ActiveServiceElement)serviceElement).StepSettings;
		}

		public ActiveServiceElement(AccountServiceElement accountElement)
			: this(accountElement.Uses.Element)
		{
			_stepSettings = accountElement.Settings;

			// Apply account service property overrides
			foreach (ConfigurationProperty property in accountElement.InnerProperties)
				ApplyProperty(accountElement, property, false);

			// Apply options
			foreach (KeyValuePair<string, string> pair in accountElement.Options)
				this.Options[pair.Key] = pair.Value;
		}

		public ActiveServiceElement(WorkflowStepElement stepElement)
			: this(stepElement.BaseConfiguration.Element)
		{
			// Apply account service property overrides
			foreach (ConfigurationProperty property in stepElement.InnerProperties)
				ApplyProperty(stepElement, property, false);

			// Apply options
			foreach (KeyValuePair<string, string> pair in stepElement.Options)
				this.Options[pair.Key] = pair.Value;
		}

		public ActiveServiceElement(AccountServiceSettingsElement settingsElement)
			: this(settingsElement.Step.Element)
		{
			// Apply settings properties
			foreach (ConfigurationProperty property in settingsElement.InnerProperties)
				ApplyProperty(settingsElement, property, false);

			// Apply options
			foreach (KeyValuePair<string, string> pair in settingsElement.Options)
				this.Options[pair.Key] = pair.Value;
		}
		/*=========================*/
		#endregion

		#region Methods
		/*=========================*/
		
		void ApplyProperty(EnabledConfigurationElement sourceElement, ConfigurationProperty sourceProperty, bool forceOverride)
		{
			object value = sourceElement[sourceProperty];
			ConfigurationProperty localProperty = this.Properties[sourceProperty.Name];

			if (localProperty == null)
				return;

			// Apply if we're forcing it to override OR if the value is not null and is not a default value
			bool apply = forceOverride ||
			//	(value == null && localProperty.DefaultValue != null) ||
				(value != null && (localProperty.DefaultValue == null || !localProperty.DefaultValue.Equals(value)));

			if (!apply)
				return;

			//if (value is SettingsCollection)
			//{
			//    SettingsCollection sourceSettings = (SettingsCollection) value;
			//    SettingsCollection localSettings = (SettingsCollection) this[localProperty];

			//    // Either overwrite the definition or merge differences only
			//    if (forceOverride)
			//    {
			//        localSettings.Clear();
			//        foreach (KeyValuePair<string, string> kv in sourceSettings)
			//            localSettings.Add(kv.Key, kv.Value);
			//    }
			//    else
			//        localSettings.Merge(sourceSettings);
			//} else
			if (value is SchedulingRuleElementCollection)
			{
				SchedulingRuleElementCollection sourceRules = (SchedulingRuleElementCollection) value;
				SchedulingRuleElementCollection localRules = (SchedulingRuleElementCollection) this[localProperty];

				if (forceOverride || (sourceRules.Overrides && sourceRules.Count > 0) || localRules == null || localRules.Count == 0)
				{
					// Replace the rule collection
					this[localProperty] = sourceRules;
				}
				else
				{
					SchedulingRuleElementCollection merged = new SchedulingRuleElementCollection();

					// Merge the rule collection
					foreach (SchedulingRuleElement rule in sourceRules)
						merged.Add(rule);

					// Merge the rule collection
					foreach (SchedulingRuleElement rule in localRules)
						merged.Add(rule);

					this[localProperty] = merged;
				}
			}
			else
			{
				// Don't change the enabled state if it were disabled
				if (localProperty.Name == "IsEnabled" && !this.IsEnabled)
					return;

				this[localProperty] = value;
			}
		}

		/*=========================*/
		#endregion

		#region Properties
		/*=========================*/

		/// <summary>
		/// Gets the collection of step settings. Available only when the ActiveServiceElement
		/// is instantiated with and AccountServiceElement.
		/// </summary>
		public AccountServiceSettingsElementCollection StepSettings
		{
			get { return _stepSettings; }
		}

		/*=========================*/
		#endregion

		#region ISerializable Members

		private ActiveServiceElement(SerializationInfo info, StreamingContext context)
		{
			
			StringReader stringReader = new StringReader(info.GetString("xml"));
			XmlTextReader reader = new XmlTextReader(stringReader);
			reader.ReadToFollowing("ActiveService");
			this.DeserializeElement(reader, false);
			this.ResolveReferences(EdgeServicesConfiguration.Current.Services, null);

			//_options = (Dictionary<string, string>)info.GetValue("options", Dictionary<string, string>);
		}

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("xml", GetXml());
		}

		public string GetXml()
		{
			StringWriter stringWriter = new StringWriter();
			XmlTextWriter writer = new XmlTextWriter(stringWriter);
			this.SerializeToXmlElement(writer, "ActiveService");
			return stringWriter.ToString();
		}

		#endregion
	}

	/// <summary>
	/// 
	/// </summary>
	public class ActiveWorkflowStepElement: WorkflowStepElement
	{
		#region Constructors
		/*=========================*/
		public ActiveWorkflowStepElement(WorkflowStepElement stepElement)
		{
			// Apply account service property overrides
			foreach (ConfigurationProperty property in stepElement.InnerProperties)
				ApplyProperty(stepElement, property, true);

			// Apply options
			foreach (KeyValuePair<string, string> pair in stepElement.Options)
				this.Options.Add(pair.Key, pair.Value);
		}


		public ActiveWorkflowStepElement(AccountServiceSettingsElement settingsElement)
			: this(settingsElement.Step.Element)
		{
			// Apply settings properties
			foreach (ConfigurationProperty property in settingsElement.InnerProperties)
				ApplyProperty(settingsElement, property, false);

			// Apply options
			foreach (KeyValuePair<string, string> pair in settingsElement.Options)
				this.Options[pair.Key] = pair.Value;
		}

		/*=========================*/
		#endregion

		#region Methods
		/*=========================*/
		
		void ApplyProperty(EnabledConfigurationElement sourceElement, ConfigurationProperty sourceProperty, bool forceOverride)
		{
			object value = sourceElement[sourceProperty];
			ConfigurationProperty localProperty = this.Properties[sourceProperty.Name];

			if (localProperty == null)
				return;

			bool apply = forceOverride || value != null;

			// This condition makes sure null is applied only if it is a valid value
			//	(value == null && localProperty.DefaultValue != null) ||
			//	(value != null && (localProperty.DefaultValue == null || !localProperty.DefaultValue.Equals(value)));

			if (!apply)
				return;

			//if (value is SettingsCollection)
			//{
			//    SettingsCollection sourceSettings = (SettingsCollection) value;
			//    SettingsCollection localSettings = (SettingsCollection) this[localProperty];

			//    // Either overwrite the definition or merge differences only
			//    if (forceOverride)
			//    {
			//        localSettings.Clear();
			//        foreach (KeyValuePair<string, string> kv in sourceSettings)
			//            localSettings.Add(kv.Key, kv.Value);
			//    }
			//    else
			//        localSettings.Merge(sourceSettings);
			//}
			//else
			//{
				// Don't change the enabled state if it were disabled
				if (localProperty.Name == "IsEnabled" && !this.IsEnabled)
					return;

				this[localProperty] = value;
			//}
		}

		/*=========================*/
		#endregion
	}
}
