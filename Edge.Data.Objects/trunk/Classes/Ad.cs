using System.Collections.Generic;
using System.Linq;

namespace Edge.Data.Objects
{
	public partial class Ad : ChannelSpecificObject
	{
		public string DestinationUrl;

		public List<TargetDefinition> TargetDefinitions;
		public CreativeDefinition CreativeDefinition
		{
			get
			{
				if (Fields == null) return null;

				return Fields.Where(x => x.Key.Name == typeof(CreativeDefinition).Name && x.Value is CreativeDefinition)
					              .Select(x => x.Value as CreativeDefinition)
								  .FirstOrDefault();
			}
		}

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			//if (CreativeDefinition != null) yield return new ObjectDimension {Value = CreativeDefinition};

			if (TargetDefinitions == null) yield break;
			foreach (var target in TargetDefinitions)
			{
				yield return new ObjectDimension {Value = target};
			}
		}
	}
}
