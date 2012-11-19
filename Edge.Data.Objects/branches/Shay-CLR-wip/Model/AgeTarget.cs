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
			public static ValueProperty<AgeTarget, Int32> FromAge = new ValueProperty<AgeTarget, Int32>("FromAge");
			public static ValueProperty<AgeTarget, Int32> ToAge = new ValueProperty<AgeTarget, Int32>("ToAge");
		}
	}
}