using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class ImageCreative
	{
		public static EntityDefinition<ImageCreative> Definition = new EntityDefinition<ImageCreative>(baseDefinition: SingleCreative.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<ImageCreative, string> ImageUrl = new EntityProperty<ImageCreative, string>("ImageUrl");
			public static EntityProperty<ImageCreative, string> ImageSize = new EntityProperty<ImageCreative, string>("ImageSize");
		}
	}
}