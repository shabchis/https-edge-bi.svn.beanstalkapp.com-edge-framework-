using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class SingleCreativeMatch
	{
		public static EntityDefinition<SingleCreativeMatch> Definition = new EntityDefinition<SingleCreativeMatch>(baseDefinition: CreativeMatch.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
		}
	}
}