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
		public Func<Segment, Dictionary<string,string>,SegmentValue> OnCreateValueFromFragments { get; set; }

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
				throw new ArgumentException(String.Format("The segment '{0}' was not found in the {1} configuration.", segment.Name, AutoSegmentDefinitionCollection.ExtensionName), "segmentName");

			var fragmentValues = new Dictionary<string,string>();

			// Find a definition that works
			for (int p = 0; p < def.Patterns.Count; p++)
			{
				// reset because previous iteration found nothing
				fragmentValues.Clear();

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
							fragmentValues[pattern.Fragments[g]] = group.Value;
					}
				}

				// found all the values, create a segment value
				if (fragmentValues.Count == pattern.Fragments.Length)
					return CreateValueFromFragments(segment, fragmentValues);
			}

			// found nothing
			return null;
		}

		public Dictionary<Segment, SegmentValue> ExtractSegmentValues(Segment[] segments, string source)
		{
			throw new NotImplementedException();
		}


		private StringBuilder _stringBuilder = new StringBuilder();
		private SegmentValue CreateValueFromFragments(Segment segment, Dictionary<string, string> fragments)
		{
			if (fragments == null || fragments.Count < 1)
				return null;

			// Custom segment generation
			if (OnCreateValueFromFragments != null)
				return OnCreateValueFromFragments(segment, fragments);
			
			// Serialize multiple values as json
			string originalID;
			using (var writer = new StringWriter(new StringBuilder()))
			{
				_serializer.Serialize(writer, fragments);
				originalID = writer.ToString();
			}

			// For a single fragment, return only the value without
			return new SegmentValue() { OriginalID = originalID, Value = fragments.Count > 1 ? originalID : fragments.First().Value };
		}
	}
}


