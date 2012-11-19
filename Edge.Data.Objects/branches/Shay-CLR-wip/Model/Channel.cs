using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Channel
	{
		public static EntityDefinition<Channel> Definition = new EntityDefinition<Channel>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<Channel, Int32> ID = new ValueProperty<Channel, Int32>("ID");
			public static ValueProperty<Channel, String> Name = new ValueProperty<Channel, String>("Name");
			public static ValueProperty<Channel, ChannelType> ChannelType = new ValueProperty<Channel, ChannelType>("ChannelType");
		}
	}
}