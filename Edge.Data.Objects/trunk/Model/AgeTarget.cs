using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class AgeTarget
	{
		public static EntityDefinition<AgeTarget> Definition = new EntityDefinition<AgeTarget>(baseDefinition: Target.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<AgeTarget, int> FromAge = new EntityProperty<AgeTarget, int>("FromAge");
			public static EntityProperty<AgeTarget, int> ToAge = new EntityProperty<AgeTarget, int>("ToAge");
		}
	}
}