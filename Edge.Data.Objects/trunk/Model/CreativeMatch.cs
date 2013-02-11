using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CreativeMatch
	{
		public static EntityDefinition<CreativeMatch> Definition = new EntityDefinition<CreativeMatch>(baseDefinition: CreativeReference.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<CreativeMatch, CreativeDefinition> CreativeDefinition = new EntityProperty<CreativeMatch, CreativeDefinition>("CreativeDefinition");
		}
	}
}