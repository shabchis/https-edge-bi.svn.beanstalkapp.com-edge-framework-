using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Configuration;
using System.Configuration;

namespace Edge.Data.Pipeline.Configuration
{
	public class RegexElementCollection : ConfigurationElementCollectionBase<RegexElement>
	{
		public override ConfigurationElementCollectionType CollectionType
		{
			get { return ConfigurationElementCollectionType.BasicMap; }
		}

		protected override string ElementName
		{
			get { return "Regex"; }
		}

		protected override ConfigurationElement CreateNewElement()
		{
			return new RegexElement();
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			// Get index name
			return (element as RegexElement).Pattern;
		}
	}

	public class RegexElement:ConfigurationElement
	{
		[ConfigurationProperty("Pattern", IsRequired=true, IsKey=true)]
		public string Pattern
		{
			get { return (string)this["Pattern"]; }
			set { this["Pattern"] = value; }
		}
	}

}
