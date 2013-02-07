using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CompositePartField
	{
		public static EntityDefinition<CompositePartField> Definition = new EntityDefinition<CompositePartField>(baseDefinition: EdgeField.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<EdgeField, Channel> Channel = new EntityProperty<EdgeField, Channel>("Channel");
		}
	}
}