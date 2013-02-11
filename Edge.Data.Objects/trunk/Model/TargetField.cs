using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class TargetField
	{
		public static EntityDefinition<TargetField> Definition = new EntityDefinition<TargetField>(baseDefinition: EdgeField.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<TargetField, Channel> Channel = new EntityProperty<TargetField, Channel>("Channel");
		}
	}
}