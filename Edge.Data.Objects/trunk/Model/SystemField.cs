using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class SystemField
	{
		public static EntityDefinition<SystemField> Definition = new EntityDefinition<SystemField>(baseDefinition: EdgeField.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
		}
	}
}