using System;
using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class CompositeCreativeMatch : CreativeMatch
	{
		public Dictionary<CompositePartField, SingleCreativeMatch> CreativesMatches;

		protected override Type CreativeType
		{
			get { return typeof(CompositeCreative); }
		}

		//public new CompositeCreative Creative
		//{
		//	get { return base.Creative as CompositeCreative; }
		//	//set { base.Creative = value; }
		//}

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (CreativesMatches == null) yield break;
			foreach (var match in CreativesMatches)
			{
				yield return new ObjectDimension { Field = match.Key, Value = match.Value };
			}
		}
	}
}
