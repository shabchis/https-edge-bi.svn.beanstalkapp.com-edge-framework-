using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class TextCreativeMatch
	{
		public static EntityDefinition<TextCreativeMatch> Definition = new EntityDefinition<TextCreativeMatch>(baseDefinition: SingleCreativeMatch.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
		}
	}
}