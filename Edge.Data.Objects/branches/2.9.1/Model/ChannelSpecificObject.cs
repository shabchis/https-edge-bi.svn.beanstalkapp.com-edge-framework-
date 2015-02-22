using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class ChannelSpecificObject
	{
		public static EntityDefinition<ChannelSpecificObject> Definition = new EntityDefinition<ChannelSpecificObject>(baseDefinition: EdgeObject.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ReferenceProperty<ChannelSpecificObject, Channel> Channel = new ReferenceProperty<ChannelSpecificObject, Channel>("Channel");
			public static ValueProperty<ChannelSpecificObject, String> OriginalID = new ValueProperty<ChannelSpecificObject, String>("OriginalID");
			public static ValueProperty<ChannelSpecificObject, ObjectStatus> Status = new ValueProperty<ChannelSpecificObject, ObjectStatus>("Status");
		}
	}
}