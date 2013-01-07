using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class CompositeCreativeMatch : CreativeMatch
	{
		public Dictionary<CompositePartField, SingleCreativeMatch> CreativesMatches;

		protected override Type CreativeType
		{
			get { return typeof(CompositeCreative); }
		}

		public new CompositeCreative Creative
		{
			get { return (CompositeCreative)base.Creative; }
			set { base.Creative = value; }
		}
	}
}
