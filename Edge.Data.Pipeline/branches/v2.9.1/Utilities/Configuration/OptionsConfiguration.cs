using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Edge.Core.Configuration;

namespace Edge.Data.Pipeline.Configuration
{
	public class OptionDefinitionCollection : ConfigurationElementCollectionBase<OptionDefinition>
	{
		public static string ExtensionName = "OptionDefinitions";

		public override ConfigurationElementCollectionType CollectionType
		{
			get { return ConfigurationElementCollectionType.BasicMap; }
		}

		protected override string ElementName
		{
			get { return "Option"; }
		}

		protected override ConfigurationElement CreateNewElement()
		{
			return new OptionDefinition();
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			// Get index name
			return (element as OptionDefinition).Name;
		}

		public new OptionDefinition this[string name]
		{
			get { return (OptionDefinition)this.BaseGet(name); }
		}
	}

	public class OptionDefinition : ConfigurationElement
	{
		[ConfigurationProperty("Name", IsRequired = true)]
		public string Name
		{
			get { return (string)this["Name"]; }
			set { this["Name"] = value; }
		}

		[ConfigurationProperty("Type", DefaultValue = typeof(String))]
		public Type Type
		{
			get { return (Type)this["Type"]; }
			set { this["Type"] = value; }
		}

		[ConfigurationProperty("DefaultValue")]
		public string DefaultValue
		{
			get { return (string)this["DefaultValue"]; }
			set { this["DefaultValue"] = value; }
		}

		[ConfigurationProperty("IsPublic", DefaultValue = true)]
		public bool IsPublic
		{
			get { return (bool)this["IsPublic"]; }
			set { this["IsPublic"] = value; }
		}
	}
}

