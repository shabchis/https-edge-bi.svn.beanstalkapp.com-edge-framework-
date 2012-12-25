using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class TargetDefinition
	{
		public static EntityDefinition<TargetDefinition> Definition = new EntityDefinition<TargetDefinition>(baseDefinition: EdgeObject.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<TargetDefinition, Target> Target = new EntityProperty<TargetDefinition, Target>("Target");
			public static EntityProperty<TargetDefinition, string> DestinationUrl = new EntityProperty<TargetDefinition, string>("DestinationUrl");
		}
	}
}