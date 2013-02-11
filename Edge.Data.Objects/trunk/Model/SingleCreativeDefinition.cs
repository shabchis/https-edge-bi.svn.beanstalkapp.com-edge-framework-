using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class SingleCreativeDefinition
	{
		public static EntityDefinition<SingleCreativeDefinition> Definition = new EntityDefinition<SingleCreativeDefinition>(baseDefinition: CreativeDefinition.Definition, fromReflection: true);

		public static class Properties
		{
		}
	}
}