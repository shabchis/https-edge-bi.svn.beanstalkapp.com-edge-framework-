using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Creative
	{
		public static EntityDefinition<Creative> Definition = new EntityDefinition<Creative>(baseDefinition: EdgeObject.Definition, fromReflection: true);

		public static class Properties
		{
		}
	}
}