using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class ImageCreativeMatch
	{
		public static EntityDefinition<ImageCreativeMatch> Definition = new EntityDefinition<ImageCreativeMatch>(baseDefinition: SingleCreativeMatch.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<ImageCreativeMatch, string> ImageSize = new EntityProperty<ImageCreativeMatch, string>("ImageSize");
		}
	}
}