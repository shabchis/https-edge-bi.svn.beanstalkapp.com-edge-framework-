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
		static Regex _fixRegex = new Regex(@"\(\?\{(\w+)\}");
		static string _fixReplace = @"(?<$1>";


		[ConfigurationProperty("Regex", IsRequired = true)]
		public string RegexString
		{
			get { return (string)this["Regex"]; }
			set
			{
				this["Regex"] = value;
				this.Regex = value == null ? null : new Regex(_fixRegex.Replace(value, _fixReplace), RegexOptions.ExplicitCapture);
				this.Fragments = value == null ? null : this.Regex.GetGroupNames();
			}
		}

		[ConfigurationProperty("Value")]
		public string Value
		{
			get { return (string)this["Value"]; }
			set { this["Value"] = FixFormat(value); }
		}

		[ConfigurationProperty("OriginalID")]
		public string OriginalID
		{
			get { return (string)this["OriginalID"]; }
			set { this["OriginalID"] = FixFormat(value); }
		}

		public Regex Regex
		{
			get;
			private set;
		}

		public string[] Fragments
		{
			get;
			private set;
		}

		string FixFormat(string raw)
		{
			string format = raw;
			for (int i = 0; i < this.Fragments.Length; i++)
				format = new Regex(@"\{" + this.Fragments[i] + @"([^\}]*)\}").Replace(format, "{" + i.ToString() + "$1}");

			return format;
		}
	}
}
