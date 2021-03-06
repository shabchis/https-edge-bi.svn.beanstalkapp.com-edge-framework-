﻿using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Channel
	{
		public static EntityDefinition<Channel> Definition = new EntityDefinition<Channel>(fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<Channel, int> ID = new EntityProperty<Channel, int>("ID");
			public static EntityProperty<Channel, string> Name = new EntityProperty<Channel, string>("Name");
			public static EntityProperty<Channel, string> DisplayName = new EntityProperty<Channel, string>("DisplayName");
			public static EntityProperty<Channel, ChannelType> ChannelType = new EntityProperty<Channel, ChannelType>("ChannelType");
		}
	}
}