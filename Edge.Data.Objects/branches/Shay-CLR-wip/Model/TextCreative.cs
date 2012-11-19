using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class TextCreative
	{
		public static EntityDefinition<TextCreative> Definition = new EntityDefinition<TextCreative>(baseDefinition: SingleCreative.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<TextCreative, TextCreativeType> TextType = new ValueProperty<TextCreative, TextCreativeType>("TextType");
			public static ValueProperty<TextCreative, String> Text = new ValueProperty<TextCreative, String>("Text");
		}
	}
}