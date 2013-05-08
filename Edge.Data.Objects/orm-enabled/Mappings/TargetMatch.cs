using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class TargetMatch
	{
		public new static class Mappings
		{
			public static Mapping<TargetMatch> Default = EdgeUtility.EntitySpace.CreateMapping<TargetMatch>()
				.Inherit(EdgeObject.Mappings.Default)
				.Map<Target>(TargetMatch.Properties.Target, target => target
					.MapEdgeObject("TargetGK", "TargetTypeID", "TargetClrType")
				)
				.Map<TargetDefinition>(TargetMatch.Properties.TargetDefinition, targetDef => targetDef
					.Map<long>(EdgeObject.Properties.GK, "TargetGK")
				)
			;
		}
	}
}