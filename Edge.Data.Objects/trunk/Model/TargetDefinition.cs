using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class TargetDefinition
	{
		public static EntityDefinition<TargetDefinition> Definition = new EntityDefinition<TargetDefinition>(baseDefinition: EdgeObject.Definition, fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<TargetDefinition, EdgeObject> Parent = new EntityProperty<TargetDefinition, EdgeObject>("Parent");
			public static EntityProperty<TargetDefinition, Target> Target = new EntityProperty<TargetDefinition, Target>("Target");
			public static EntityProperty<TargetDefinition, Destination> Destination = new EntityProperty<TargetDefinition, Destination>("Destination");
		}
	}
}