using System.Collections.Generic;
using System.Linq;

namespace Edge.Data.Objects
{
	public partial class CompositeCreative : Creative
	{
		//public Dictionary<CompositePartField, SingleCreative> Parts;

		public Dictionary<CompositePartField, SingleCreative> Parts
		{
			get
			{
				if (Fields == null) return null;

				return Fields.Where(x => x.Key is CompositePartField && x.Value is SingleCreative)
				                  .ToDictionary(x => x.Key as CompositePartField, x => x.Value as SingleCreative);
			}
		}

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
