using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class SingleCreative
	{
		public static EntityDefinition<SingleCreative> Definition = new EntityDefinition<SingleCreative>(baseDefinition: Creative.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
		}
	}
}