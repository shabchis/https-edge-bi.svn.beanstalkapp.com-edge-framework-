using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;
using System.Configuration;

namespace Edge.Data.Pipeline.Configuration
{
	public class ReportConfigurationElement : ConfigurationElementCollectionBase<ReportFieldElementCollection>
	{
		public override ConfigurationElementCollectionType CollectionType
		{
			get { return ConfigurationElementCollectionType.BasicMap; }
		}

		protected override string ElementName
		{
			get { return "report"; }
		}

		public new ReportFieldElementCollection this[string reportName]
		{
			get { return (ReportFieldElementCollection)this.BaseGet(reportName); }
		}

		protected override ConfigurationElement CreateNewElement()
		{
			return new ReportFieldElementCollection();
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			return (element as ReportFieldElementCollection).Name;
		}
	}


	public class ReportFieldElementCollection : ConfigurationElementCollectionBase<ReportFieldElement>
	{
		public override ConfigurationElementCollectionType CollectionType
		{
			get { return ConfigurationElementCollectionType.BasicMap; }
		}

		protected override string ElementName
		{
			get { return "field"; }
		}

		public new ReportFieldElement this[string name]
		{
			get { return (ReportFieldElement)base.BaseGet(name); }
		}

		[ConfigurationProperty("name", IsKey=true, DefaultValue="")]
		public string Name
		{
			get { return (string)this[this.Properties["name"]]; }
			set { this[this.Properties["name"]] = value; }
		}

		protected override ConfigurationElement CreateNewElement()
		{
			return new ReportFieldElement();
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			// Get index name
			return (element as ReportFieldElement).Field;
		}
	}

	public class ReportFieldElement:ConfigurationElement
	{
		[ConfigurationProperty("field", IsRequired=true, IsKey=true)]
		public string Field
		{
			get { return (string) this["field"]; }
			set { this["field"] = value; }
		}

		[ConfigurationProperty("property", IsRequired = false)]
		public string Property
		{
			get { return (string)this["property"]; }
			set { this["property"] = value; }
		}
	}
}
