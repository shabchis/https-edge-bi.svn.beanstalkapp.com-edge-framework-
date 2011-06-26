using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Edge.Core.Configuration;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Configuration;
using Newtonsoft.Json;
using System.IO;

namespace Edge.Data.Pipeline
{
	public class AutoSegmentationUtility
	{
		public AutoSegmentDefinitionCollection Definitions { get; set; }
		public Func<Segment, Dictionary<string,string>,SegmentValue> OnCreateSegmentValues { get; set; }

		JsonSerializer _serializer = new JsonSerializer();

		public AutoSegmentationUtility(AutoSegmentDefinitionCollection definitions)
		{
			this.Definitions = definitions;
		}

		public SegmentValue ExtractSegmentValue(Segment segment, string source)
		{
			if (segment == null)
				throw new ArgumentNullException("segment");

			AutoSegmentDefinition def = this.Definitions[segment.Name];
			if (def == null)
				throw new ArgumentException(String.Format("The segment '{0}' was not found in the AutoSegments configuration.", segment.Name), "segmentName");

			var values = new Dictionary<string,string>();

			// Find a definition that works
			for (int p = 0; p < def.Patterns.Count; p++)
			{
				// reset because previous iteration found nothing
				values.Clear();

				AutoSegmentPattern pattern = def.Patterns[p];

				foreach(Match match in pattern.Regex.Matches(source))
				{
					if (!match.Success)
						continue;

					for(int g = 0; g < match.Groups.Count; g++)
					{
						Group group = match.Groups[g];
						if (!group.Success)
							continue;
						else
							values[pattern.Fragments[g]] = group.Value;
					}
				}

				// found all the values, create a segment value
				if (values.Count == pattern.Fragments.Length)
					return CreateSegmentValue(segment, values);
			}

			// found nothing
			return null;
		}

		public Dictionary<Segment, SegmentValue> ExtractSegmentValues(Segment[] segments, string source)
		{
			for (int r = 0; r < this.Definitions.Count; r++)
			{
			}
			throw new NotImplementedException();
		}


		private StringBuilder _stringBuilder = new StringBuilder();
		private SegmentValue CreateSegmentValue(Segment segment, Dictionary<string, string> values)
		{
			// Custom segment generation
			if (OnCreateSegmentValues != null)
				return OnCreateSegmentValues(segment, values);

			using (var writer = new StringWriter(new StringBuilder()))
			{
				_serializer.Serialize(writer, values);
				return new SegmentValue() { Value = writer.ToString() };
			}
		}
	}
}

namespace Edge.Data.Pipeline.Configuration
{
	public class AutoSegmentDefinitionCollection : ConfigurationElementCollectionBase<AutoSegmentDefinition>
	{
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
			get { return (AutoSegmentDefinition) this.BaseGet(name); }
		}
	}

	public class AutoSegmentDefinition:ConfigurationElement
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

		[ConfigurationProperty("Patterns", IsRequired = true, IsDefaultCollection = true)]
		public AutoSegmentPatternCollection Patterns
		{
			get { return (AutoSegmentPatternCollection)this["Patterns"]; }
			set { this["Patterns"] = value; }
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
			return null;
		}

	}

	public class AutoSegmentPattern : ConfigurationElement
	{
		[ConfigurationProperty(null, IsRequired = true)]
		public string Value
		{
			get { return (string)this[(string)null]; }
			set
			{
				this[(string)null] = value;
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
