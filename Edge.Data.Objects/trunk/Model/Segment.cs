using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Segment
	{
		public static EntityDefinition<Segment> Definition = new EntityDefinition<Segment>(baseDefinition: ChannelSpecificObject.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ReferenceProperty<Segment, MetaProperty> MetaProperty = new ReferenceProperty<Segment, MetaProperty>("MetaProperty");
		}
	}
}