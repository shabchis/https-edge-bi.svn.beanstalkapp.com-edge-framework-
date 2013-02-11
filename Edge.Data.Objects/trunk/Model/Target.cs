using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Target
	{
		public static EntityDefinition<Target> Definition = new EntityDefinition<Target>(baseDefinition: EdgeObject.Definition, fromReflection: true);

		public static class Properties
		{
		}
	}
}