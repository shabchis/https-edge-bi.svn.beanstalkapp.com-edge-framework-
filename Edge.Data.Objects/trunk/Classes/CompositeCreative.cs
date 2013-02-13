using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class CompositeCreative : Creative
	{
		public Dictionary<CompositePartField, SingleCreative> Parts;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (Parts == null) yield break;
			foreach (var part in Parts)
			{
				yield return new ObjectDimension {Field = part.Key, Value = part.Value};
			}
		}
	}
}
