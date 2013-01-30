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
	}
}
