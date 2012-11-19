using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class PlacementTarget
	{
		public static EntityDefinition<PlacementTarget> Definition = new EntityDefinition<PlacementTarget>(baseDefinition: Target.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<PlacementTarget, String> Value = new ValueProperty<PlacementTarget, String>("Value");
			public static ValueProperty<PlacementTarget, PlacementType> PlacementType = new ValueProperty<PlacementTarget, PlacementType>("PlacementType");
		}
	}
}