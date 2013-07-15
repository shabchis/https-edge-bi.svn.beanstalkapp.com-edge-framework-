using System;
using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class Ad : ChannelSpecificObject
	{
		//public string DestinationUrl;
		public Destination MatchDestination;
		public Dictionary<TargetField, TargetDefinition> TargetDefinitions;
		public CreativeMatch CreativeMatch;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (MatchDestination != null) yield return new ObjectDimension
			{
				Field = EdgeType["MatchDestination"],
				Value = MatchDestination
			};

			if (CreativeMatch != null) yield return new ObjectDimension
			{
				Field = EdgeType[String.Format("{0}_CreativeMatch", EdgeType.Name)],
				Value = CreativeMatch
			};

			if (TargetDefinitions == null) yield break;
			foreach (var target in TargetDefinitions)
			{
				yield return new ObjectDimension {Field = target.Key, Value = target.Value};
			}
		}
	}
}
