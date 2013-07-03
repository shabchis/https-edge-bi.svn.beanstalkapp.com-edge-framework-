using System.Collections.Generic;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Ad
	{
		public static EntityDefinition<Ad> Definition = new EntityDefinition<Ad>(baseDefinition: ChannelSpecificObject.Definition, fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<Ad, string> DestinationUrl = new EntityProperty<Ad, string>("DestinationUrl");
			public static EntityProperty<Ad, List<TargetDefinition>> TargetDefinitions = new EntityProperty<Ad, List<TargetDefinition>>("TargetDefinitions");
			public static EntityProperty<Ad, CreativeMatch> CreativeMatch = new EntityProperty<Ad, CreativeMatch>("CreativeMatch");
		}
	}
}