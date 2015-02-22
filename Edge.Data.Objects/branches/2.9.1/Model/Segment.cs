using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class PropertyOption
	{
		public static EntityDefinition<PropertyOption> Definition = new EntityDefinition<PropertyOption>(baseDefinition: ChannelSpecificObject.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ReferenceProperty<PropertyOption, ConnectionDefinition> MetaProperty = new ReferenceProperty<PropertyOption, ConnectionDefinition>("MetaProperty");
		}
	}
}