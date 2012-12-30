using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class TargetDefinition
	{
		public new static class Mappings
		{
			public static Mapping<TargetDefinition> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<TargetDefinition>()
				.Inherit(EdgeObject.Mappings.Default)
				.Map<Target>(TargetDefinition.Properties.Target, target => target
					.MapEdgeObject("TargetGK", "TargetTypeID", "TargetClrType")
				)
			;
		}
	}
}