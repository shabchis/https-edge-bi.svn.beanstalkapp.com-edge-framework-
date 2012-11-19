using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class GenderTarget
	{
		public static EntityDefinition<GenderTarget> Definition = new EntityDefinition<GenderTarget>(baseDefinition: Target.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<GenderTarget, Gender> Gender = new ValueProperty<GenderTarget, Gender>("Gender");
		}
	}
}