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
			public static EntityProperty<PlacementTarget, string> Value = new EntityProperty<PlacementTarget, string>("Value");
			public static EntityProperty<PlacementTarget, PlacementType> PlacementType = new EntityProperty<PlacementTarget, PlacementType>("PlacementType");
		}
	}
}