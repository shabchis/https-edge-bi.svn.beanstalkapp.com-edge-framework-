using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Ad
	{
		public new static EntityDefinition<Ad> Definition = new EntityDefinition<Ad>(ChannelSpecificObject.Definition, fromReflection: typeof(Properties));

		public new class Properties
		{
			public static ValueProperty<Ad, string> DestinationUrl = new ValueProperty<Ad, string>("DestinationUrl");
			public static ReferenceProperty<Ad, Creative> Creative = new ReferenceProperty<Ad, Creative>("Creative");
		}
	}
}
