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
			public static ReferenceProperty<TargetMatch, Target> Target = new ReferenceProperty<TargetMatch, Target>("Target");
			public static ValueProperty<TargetMatch, String> DestinationUrl = new ValueProperty<TargetMatch, String>("DestinationUrl");
		}
	}
}