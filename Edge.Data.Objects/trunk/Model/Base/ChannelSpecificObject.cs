using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class ChannelSpecificObject
	{
		public new static EntityDefinition<ChannelSpecificObject> Definition = new EntityDefinition<ChannelSpecificObject>(EdgeObject.Definition, fromReflection: typeof(Properties));

		public new class Properties
		{
			public static ReferenceProperty<ChannelSpecificObject, Channel> Channel = new ReferenceProperty<ChannelSpecificObject, Channel>("Channel");
			public static ValueProperty<ChannelSpecificObject, ObjectStatus> Status = new ValueProperty<ChannelSpecificObject, ObjectStatus>("Status");
			public static ValueProperty<ChannelSpecificObject, string> OriginalID = new ValueProperty<ChannelSpecificObject, string>("OriginalID");

		}
	}
}
