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
			public static ValueProperty<Ad, String> DestinationUrl = new ValueProperty<Ad, String>("DestinationUrl");
			public static ReferenceProperty<Ad, Creative> Creative = new ReferenceProperty<Ad, Creative>("Creative");
		}
	}
}