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
			public static Mapping<CompositeCreative> Default = EdgeUtility.EntitySpace.CreateMapping<CompositeCreative>(creative => creative
				.Inherit(Creative.Mappings.Default)

				/*
				.Map<Dictionary<string, SingleCreative>>(CompositeCreative.Properties.Parts, parts => parts
					.Subquery("Parts", subquery=>subquery
						.Map<CompositeCreative>("parent", parent => parent
							.Map<long>(EdgeObject.Properties.GK, "CompositeGK")
						)
						.Map<string>("key", "PartRole")
						.Map<SingleCreative>("value", value => value
							.MapEdgeObject("PartGK", "PartTypeID", "PartClrType")
						)
						.Do(context => CompositeCreative.Properties.Parts.GetValue(context.GetVariable<CompositeCreative>("parent")).Add(
								context.GetVariable<string>("key"),
								context.GetVariable<SingleCreative>("value")
							)
						)
					)
				)
				*/
				/*
				.MapDictionaryFromSubquery<CompositeCreative, string, SingleCreative>(CompositeCreative.Properties.Parts, "Parts",
					parent => parent
						.Map<long>(EdgeObject.Properties.GK, "CompositeGK"),
					key => key
						.Set(context => context.GetField<string>("PartRole")),
					value => value
						.MapEdgeObject("PartGK", "PartTypeID", "PartClrType")
				)
				*/
			);
		}
	}
}
