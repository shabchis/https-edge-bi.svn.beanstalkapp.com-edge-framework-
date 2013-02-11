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
			public static EntityProperty<ImageCreative, string> Image = new EntityProperty<ImageCreative, string>("Image");
		}
	}
}