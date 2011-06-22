using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline
{

	public class AutoSegmentationEngine
	{
		public Regex[] Patterns {get; set;}
		public AutoSegmentationDefinition[] Definitions {get; set;}
		public Func<Segment, Dictionary<string, string>, SegmentValue> OnCreateSegmentValues { get; set; }

		public void Begin()
		{
			foreach (AutoSegmentationDefinition def in this.Definitions)
			{
				def.TempValues = new Dictionary<string, string>();
				foreach (string param in def.RequiredParameters)
					def.TempValues.Add(param, null);
			}
		}

		// Performance is key! will be run millions of times a day.
		public Dictionary<Segment, SegmentValue> ExtractSegmentsFromString(string source)
		{
			var output = new Dictionary<Segment, SegmentValue>();

			for (int r = 0; r < this.Patterns.Length; r++ )
			{
				Regex regex = this.Patterns[r];
				Match match = regex.Match(source);
				if (!match.Success)
					continue;

				// Find a definition that works
				for (int d = 0; d < this.Definitions.Length; d++)
				{
					AutoSegmentationDefinition def = this.Definitions[d];

					// Make sure all required parameters are present, and save them to temp. values if they are
					if (!def.RequiredParameters.All(param => {
						Group gr = match.Groups[param];
						bool success = gr != null && gr.Success;
						if (success)
							def.TempValues[param] = gr.Value;
						return success;
					}))
						continue;

					SegmentValue value = CreateSegmentValue(def.Segment, def.TempValues);
					if (value != null)
						output[def.Segment] = value;
				}

				// Break because we don't want to match more than one pattern
				break;
			}

			return output;
		}

		private StringBuilder _stringBuilder = new StringBuilder();
		private SegmentValue CreateSegmentValue(Segment segment, Dictionary<string, string> values)
		{
			// Custom segment generation
			if (OnCreateSegmentValues != null)
				return OnCreateSegmentValues(segment, values);
			return null;
			/*
			_stringBuilder.Clear();
			foreach (var pair in values)
			{
				_stringBuilder.AppendFormat("{0}={1}__"
			}
			*/
		}
	}

	public class AutoSegmentationDefinition
	{
		public string[] RequiredParameters { get; set; }
		public Segment Segment {get; set;}

		// For internal, temporary use only.
		internal Dictionary<string, string> TempValues;

	}

	#region Made obsolete by shifting to segments
	/*
	public class Tracker
	{
		public Dictionary<string,string> Values { get; set; }
		
		Ad _ad;
		List<Target> _targets;

		public Ad Ad
		{
			get
			{
				if (_targets != null && _targets.Count > 0)
					throw new InvalidOperationException("Tracker ad is not available when the tracker has targeting data.");
				return _ad;
			}
			set
			{
				if (_targets != null && _targets.Count > 0)
					throw new InvalidOperationException("Tracker ad is not available when the tracker has targeting data.");

				_ad = value;
			}
		}

		public List<Target> Targets
		{
			get
			{
				if (this.Ad != null)
					throw new InvalidOperationException("Tracker targets are not available when the tracker is associated with an ad.");

				return _targets ?? (_targets = new List<Target>());
			}
		}

		public Tracker(Ad ad)
		{
			Ad = ad;
		}
	}
	*/
	#endregion

}
