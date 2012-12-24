using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class CompositeCreative
	{
		public new static class Mappings
		{
			/*
			public static Mapping<CompositeCreative> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<CompositeCreative>(creative => creative
				.Inherit(Creative.Mappings.Default)

				.Map<Dictionary<string, SingleCreative>>(CompositeCreative.Properties.ChildCreatives, childCreatives => childCreatives
					.Subquery("ChildCreatives", subquery=>subquery
						.Map<CompositeCreative>("parent", parent => parent
							.Map<long>(
			);
			*/
		}
	}
}
