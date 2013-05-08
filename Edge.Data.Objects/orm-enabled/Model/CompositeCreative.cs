using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CompositeCreative
	{
		public static EntityDefinition<CompositeCreative> Definition = new EntityDefinition<CompositeCreative>(baseDefinition: Creative.Definition, fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<CompositeCreative, Dictionary<Edge.Data.Objects.CompositePartField, Edge.Data.Objects.SingleCreative>> Parts = new EntityProperty<CompositeCreative, Dictionary<Edge.Data.Objects.CompositePartField, Edge.Data.Objects.SingleCreative>>("Parts");
		}
	}
}