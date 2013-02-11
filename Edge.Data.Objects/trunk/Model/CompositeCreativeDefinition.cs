using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CompositeCreativeDefinition
	{
		public static EntityDefinition<CompositeCreativeDefinition> Definition = new EntityDefinition<CompositeCreativeDefinition>(baseDefinition: CreativeDefinition.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<CompositeCreativeDefinition, Dictionary<Edge.Data.Objects.CompositePartField, Edge.Data.Objects.SingleCreativeDefinition>> CreativeDefinitions = new EntityProperty<CompositeCreativeDefinition, Dictionary<Edge.Data.Objects.CompositePartField, Edge.Data.Objects.SingleCreativeDefinition>>("CreativeDefinitions");
		}
	}
}