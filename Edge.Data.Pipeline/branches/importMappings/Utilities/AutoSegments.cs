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
		public event EventHandler<AutoSegmentFoundEventArgs> SegmentFound;

		JsonSerializer _serializer = new JsonSerializer();

		public AutoSegmentationUtility(AutoSegmentDefinitionCollection definitions)
		{
			if (!definitions.IsEnabled)
				this.Definitions = null;
			else
				this.Definitions = definitions;
		}

		public SegmentValue ExtractSegmentValue(Segment segment, string source)
		{
			if (this.Definitions == null)
				return null;

			if (segment == null)
				throw new ArgumentNullException("segment");

			if (source == null)
				throw new ArgumentNullException("source", "Segments can only be extracted from a non-null source.");

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

				MatchCollection matches = pattern.Regex.Matches(source);
				foreach (Match match in matches)
				{
					if (!match.Success)
						continue;

					int fragmentCounter = 0;
					for(int g = 0; g < match.Groups.Count; g++)
					{
						Group group = match.Groups[g];
						string groupName = pattern.RawGroupNames[g];
						if (!group.Success || !AutoSegmentPattern.IsValidFragmentName(groupName))
							continue;

						// Save the fragment
						fragmentValues[pattern.Fragments[fragmentCounter++]] = group.Value;
					}
				}

				// found all the values, create a segment value
				if (fragmentValues.Count == pattern.Fragments.Length)
					return CreateValueFromFragments(segment, fragmentValues, pattern);
			}

			// found nothing
			return null;
		}

		public Dictionary<Segment, SegmentValue> ExtractSegmentValues(Segment[] segments, string source)
		{
			throw new NotImplementedException();
		}

		private string JsonSerialize(Dictionary<string, string> fragments)
		{
			using (var writer = new StringWriter(new StringBuilder()))
			{
				_serializer.Serialize(writer, fragments);
				return writer.ToString();
			}
		}


		private StringBuilder _stringBuilder = new StringBuilder();
		private SegmentValue CreateValueFromFragments(Segment segment, Dictionary<string, string> fragments, AutoSegmentPattern pattern)
		{
			if (fragments == null || fragments.Count < 1)
				return null;

			// Custom segment generation
			if (SegmentFound != null)
			{
				var e = new AutoSegmentFoundEventArgs() { Segment = segment, Fragments = fragments, Pattern = pattern };
				SegmentFound(this, e);
				if (e.Value != null)
					return e.Value;
			}

			string originalID = null;
			string value;

			// EVERYBODY DO THE FORMATTING DANCE

			originalID = !String.IsNullOrWhiteSpace(pattern.OriginalID) ?
				// use custom format
				String.Format(pattern.OriginalID, fragments.Values.ToArray())
			:
				// json serialize fragments if no custom format defined
				originalID = JsonSerialize(fragments)
			;

			value = !String.IsNullOrWhiteSpace(pattern.Value) ?
				// use custom format
				String.Format(pattern.Value, fragments.Values.ToArray())
			:
				// no custom format
				value = fragments.Count == 1 ?
					// one fragment only, just use it
					fragments.First().Value
				:
					// json serialize fragments (re-use originalID if it was serialized above)
					(originalID != null ?
						originalID :
						JsonSerialize(fragments)
					)
			;

			// Return the value!
			return new SegmentValue() { OriginalID = originalID, Value = value };
		}
	}

	public class AutoSegmentFoundEventArgs : EventArgs
	{
		public Segment Segment { get; internal set; }
		public Dictionary<string, string> Fragments {get; internal set;}
		public AutoSegmentPattern Pattern { get; internal set; }
		public SegmentValue Value { get; set; }
	}
}


