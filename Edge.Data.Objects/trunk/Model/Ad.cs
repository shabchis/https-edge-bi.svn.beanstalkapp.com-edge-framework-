using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Ad
	{
		public static EntityDefinition<Ad> Definition = new EntityDefinition<Ad>(baseDefinition: ChannelSpecificObject.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<Ad, string> DestinationUrl = new EntityProperty<Ad, string>("DestinationUrl");
			public static EntityProperty<Ad, Creative> Creative = new EntityProperty<Ad, Creative>("Creative");
		}
	}
}