using System;
using System.Collections.Generic;
using System.Linq;

namespace Edge.Data.Objects
{
	public partial class CompositeCreativeDefinition : CreativeDefinition
	{
		//public Dictionary<CompositePartField, SingleCreativeDefinition> CreativeDefinitions;

		protected override Type CreativeType
		{
			get { return typeof(CompositeCreative); }
		}

		public override Creative Creative
		{
			get { return (CompositeCreative)base.Creative; }
			set { base.Creative = value; }
		}

		public Dictionary<CompositePartField, SingleCreativeDefinition> CreativeDefinitions
		{
			get
			{
				if (ExtraFields == null) return null;

				return ExtraFields.Where(x => x.Key is CompositePartField && x.Value is SingleCreativeDefinition)
								  .ToDictionary(x => x.Key as CompositePartField, x => x.Value as SingleCreativeDefinition);
			}
		}

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (Creative != null) yield return new ObjectDimension {Value = Creative};

			if (CreativeDefinitions == null) yield break;
			foreach (var definition in CreativeDefinitions)
			{
				yield return new ObjectDimension {Field = definition.Key, Value = definition.Value};
			}
		}
	}
}
