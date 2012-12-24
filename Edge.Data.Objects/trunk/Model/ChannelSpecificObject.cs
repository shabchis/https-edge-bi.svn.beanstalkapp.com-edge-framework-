using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class ChannelSpecificObject
	{
		public new static EntityDefinition<ChannelSpecificObject> Definition = new EntityDefinition<ChannelSpecificObject>(baseDefinition: EdgeObject.Definition, fromReflection: typeof(Properties));

		public new static class Properties
		{
			public static EntityProperty<ChannelSpecificObject, Channel> Channel = new EntityProperty<ChannelSpecificObject, Channel>("Channel");
			public static EntityProperty<ChannelSpecificObject, string> OriginalID = new EntityProperty<ChannelSpecificObject, string>("OriginalID");
			public static EntityProperty<ChannelSpecificObject, ObjectStatus> Status = new EntityProperty<ChannelSpecificObject, ObjectStatus>("Status");
		}
	}
}