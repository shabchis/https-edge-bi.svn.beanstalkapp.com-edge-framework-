using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CreativeDefinition
	{
		public static EntityDefinition<CreativeDefinition> Definition = new EntityDefinition<CreativeDefinition>(baseDefinition: CreativeReference.Definition, fromReflection: true);

		public static class Properties
		{
		}
	}
}