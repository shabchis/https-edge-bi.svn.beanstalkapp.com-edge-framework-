using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CreativeReference
	{
		public static EntityDefinition<CreativeReference> Definition = new EntityDefinition<CreativeReference>(baseDefinition: ChannelSpecificObject.Definition, fromReflection: true);

		public static class Properties
		{
			//public static EntityProperty<CreativeReference, EdgeObject> Parent = new EntityProperty<CreativeReference, EdgeObject>("Parent");
			public static EntityProperty<CreativeReference, Destination> Destination = new EntityProperty<CreativeReference, Destination>("Destination");
		}
	}
}