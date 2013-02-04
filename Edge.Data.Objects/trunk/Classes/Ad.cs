using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class Ad : ChannelSpecificObject
	{
		public string DestinationUrl;

		public List<TargetDefinition> TargetDefinitions;
		public CreativeDefinition CreativeDefinition;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			if (CreativeDefinition != null) yield return new ObjectDimension {Value = CreativeDefinition};

			if (TargetDefinitions == null) yield break;
			foreach (var target in TargetDefinitions)
			{
				yield return new ObjectDimension {Value = target};
			}
		}
	}
}
