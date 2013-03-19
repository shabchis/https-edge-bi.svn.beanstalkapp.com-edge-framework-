using System;
using System.Collections.Generic;
using System.Linq;

namespace Edge.Data.Objects
{
	public partial class CompositeCreativeDefinition : CreativeDefinition
	{
		public Dictionary<CompositePartField, SingleCreativeDefinition> CreativeDefinitions;

		protected override Type CreativeType
		{
			get { return typeof(CompositeCreative); }
		}

		//public CompositeCreative Creative
		//{
		//	get { return base.Creative as CompositeCreative; }
		//	set { base.Creative = value; }
		//}

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (CreativeDefinitions == null) yield break;
			foreach (var definition in CreativeDefinitions)
			{
				yield return new ObjectDimension {Field = definition.Key, Value = definition.Value};
			}
		}
	}
}
