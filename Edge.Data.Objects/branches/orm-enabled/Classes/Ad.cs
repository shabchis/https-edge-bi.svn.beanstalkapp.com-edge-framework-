using System;
using System.Collections.Generic;
using System.Linq;

namespace Edge.Data.Objects
{
	public partial class Ad : ChannelSpecificObject
	{
		public string DestinationUrl;

		public List<TargetDefinition> TargetDefinitions;
		public CreativeDefinition CreativeDefinition;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (CreativeDefinition != null) yield return new ObjectDimension
			{
				Field = this.EdgeType[String.Format("{0}_CreativeDefinition", this.EdgeType.Name)],
				Value = CreativeDefinition
			};

			if (TargetDefinitions == null) yield break;
			foreach (var target in TargetDefinitions)
			{
				// TODO: set edge field for Traget definition, may be should be dictionary
				yield return new ObjectDimension {Value = target};
			}
		}
	}
}
