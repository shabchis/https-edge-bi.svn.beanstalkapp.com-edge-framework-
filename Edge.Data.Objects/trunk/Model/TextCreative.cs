using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class TextCreative
	{
		public static EntityDefinition<TextCreative> Definition = new EntityDefinition<TextCreative>(baseDefinition: SingleCreative.Definition, fromReflection: true);

		public static class Properties
		{
			//public static EntityProperty<TextCreative, TextCreativeType> TextCreativeType = new EntityProperty<TextCreative, TextCreativeType>("TextCreativeType");
			public static EntityProperty<TextCreative, string> Text = new EntityProperty<TextCreative, string>("Text");
		}
	}
}