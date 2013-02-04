using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class CompositeCreativeDefinition : CreativeDefinition
	{
		public Dictionary<CompositePartField, SingleCreativeDefinition> CreativeDefinitions;

		protected override Type CreativeType
		{
			get { return typeof(CompositeCreative); }
		}

		public override Creative Creative
		{
			get { return (CompositeCreative)base.Creative; }
			set { base.Creative = value; }
		}

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			if (Creative != null) yield return new ObjectDimension {Value = Creative};

			if (CreativeDefinitions == null) yield break;
			foreach (var definition in CreativeDefinitions)
			{
				yield return new ObjectDimension {Field = definition.Key, Value = definition.Value};
			}
		}
	}
}
