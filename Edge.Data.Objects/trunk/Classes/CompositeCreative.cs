using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class CompositeCreative : Creative
	{
		public Dictionary<CompositePartField, SingleCreative> Parts;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			if (Parts == null) yield break;
			foreach (var part in Parts)
			{
				yield return new ObjectDimension {Field = part.Key, Value = part.Value};
			}
		}
	}
}
