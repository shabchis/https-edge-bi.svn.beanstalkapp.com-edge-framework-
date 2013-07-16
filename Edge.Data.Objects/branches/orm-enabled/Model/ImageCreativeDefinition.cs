using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class ImageCreativeDefinition
	{
		public static EntityDefinition<ImageCreativeDefinition> Definition = new EntityDefinition<ImageCreativeDefinition>(baseDefinition: SingleCreativeDefinition.Definition, fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<ImageCreativeDefinition, string> ImageSize = new EntityProperty<ImageCreativeDefinition, string>("ImageSize");
		}
	}
}