using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class GenderTarget
	{
		public static EntityDefinition<GenderTarget> Definition = new EntityDefinition<GenderTarget>(baseDefinition: Target.Definition, fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<GenderTarget, Gender> Gender = new EntityProperty<GenderTarget, Gender>("Gender");
		}
	}
}