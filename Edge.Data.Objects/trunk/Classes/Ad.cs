using System;
using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class Ad : ChannelSpecificObject
	{
		public string DestinationUrl;

		public List<TargetDefinition> TargetDefinitions;
		public CreativeMatch CreativeMatch;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (CreativeMatch != null) yield return new ObjectDimension
			{
				Field = EdgeType[String.Format("{0}_CreativeMatch", EdgeType.Name)],
				Value = CreativeMatch
			};

			if (TargetDefinitions == null) yield break;
			foreach (var target in TargetDefinitions)
			{
				// TODO: set edge field for Target definition, may be should be dictionary
				yield return new ObjectDimension {Value = target};
			}
		}
	}
}
