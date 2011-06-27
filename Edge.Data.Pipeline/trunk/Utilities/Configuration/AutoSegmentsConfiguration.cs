using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Edge.Core.Configuration;

namespace Edge.Data.Pipeline.Configuration
{
	public class AutoSegmentDefinitionCollection : ConfigurationElementCollectionBase<AutoSegmentDefinition>
	{
		public static string ExtensionName = "AutoSegments";

		public override ConfigurationElementCollectionType CollectionType
		{
			get { return ConfigurationElementCollectionType.BasicMap; }
		}

		protected override string ElementName
		{
			get { return "Segment"; }
		}

		protected override ConfigurationElement CreateNewElement()
		{
			return new AutoSegmentDefinition();
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			// Get index name
			return (element as AutoSegmentDefinition).Name;
		}

		public new AutoSegmentDefinition this[string name]
		{
			get { return (AutoSegmentDefinition)this.BaseGet(name); }
		}
	}

	public class AutoSegmentDefinition : ConfigurationElement
	{
		[ConfigurationProperty("Name", IsRequired = true)]
		public string Name
		{
			get { return (string)this["Name"]; }
			set { this["Name"] = value; }
		}

		[ConfigurationProperty("SegmentID", IsRequired = true)]
		public int SegmentID
		{
			get { return (int)this["SegmentID"]; }
			set { this["SegmentID"] = value; }
		}

		[ConfigurationProperty("", IsRequired = true, IsDefaultCollection = true)]
		public AutoSegmentPatternCollection Patterns
		{
			get { return (AutoSegmentPatternCollection)this[""]; }
		}
	}

	public class AutoSegmentPatternCollection : ConfigurationElementCollectionBase<AutoSegmentPattern>
	{
		public override ConfigurationElementCollectionType CollectionType
		{
			get { return ConfigurationElementCollectionType.BasicMap; }
		}

		protected override string ElementName
		{
			get { return "Pattern"; }
		}

		protected override ConfigurationElement CreateNewElement()
		{
			return new AutoSegmentPattern();
		}

		protected override object GetElementKey(ConfigurationElement element)
		{
			return ((AutoSegmentPattern)element).Value;
		}
	}

	public class AutoSegmentPattern : ConfigurationElement
	{
		[ConfigurationProperty("Value", IsRequired = true)]
		public string Value
		{
			get { return (string)this[(string)"Value"]; }
			set
			{
				this[(string)"Value"] = value;
				_regex = null;
				_fragments = null;
			}
		}

		Regex _regex = null;
		string[] _fragments = null;

		public Regex Regex
		{
			get
			{
				if (_regex == null)
				{
					_regex = new Regex(this.Value, RegexOptions.ExplicitCapture);
				}
				return _regex;
			}
		}

		public string[] Fragments
		{
			get { return _fragments; }
		}
	}
}
