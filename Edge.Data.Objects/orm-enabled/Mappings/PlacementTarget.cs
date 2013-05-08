using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class PlacementTarget
	{
		public new static class Mappings
		{
			public static Mapping<PlacementTarget> Default = EdgeUtility.EntitySpace.CreateMapping<PlacementTarget>()
				.Inherit(Target.Mappings.Default)
				.Map<PlacementType>(PlacementTarget.Properties.PlacementType, "int_Field1")
				.Map<string>(PlacementTarget.Properties.Value, "string_Field1")
			;
		}
	}
}
