using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class TargetMatch
	{
		public static EntityDefinition<TargetMatch> Definition = new EntityDefinition<TargetMatch>(baseDefinition: EdgeObject.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<TargetMatch, EdgeObject> Parent = new EntityProperty<TargetMatch, EdgeObject>("Parent");
			public static EntityProperty<TargetMatch, Target> Target = new EntityProperty<TargetMatch, Target>("Target");
			public static EntityProperty<TargetMatch, TargetDefinition> TargetDefinition = new EntityProperty<TargetMatch, TargetDefinition>("TargetDefinition");
			public static EntityProperty<TargetMatch, string> DestinationUrl = new EntityProperty<TargetMatch, string>("DestinationUrl");
		}
	}
}