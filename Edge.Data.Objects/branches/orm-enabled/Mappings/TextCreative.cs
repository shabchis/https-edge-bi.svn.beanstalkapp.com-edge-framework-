using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class TextCreative
	{
		public new static class Mappings
		{
			public static Mapping<TextCreative> Default = EdgeUtility.EntitySpace.CreateMapping<TextCreative>()
				.Inherit(SingleCreative.Mappings.Default)
				.Map<TextCreativeType>(TextCreative.Properties.TextType, "int_Field1")
				.Map<string>(TextCreative.Properties.Text, "string_Field1")
			;
		}
	}
}