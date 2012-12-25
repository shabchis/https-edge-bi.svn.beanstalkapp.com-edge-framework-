using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class StringValue
	{
		public static class Mappings
		{
			public static Mapping<StringValue> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<StringValue>()
				.Inherit(ChannelSpecificObject.Mappings.Default)
				.Map<string>(StringValue.Properties.Value, "string_Field1")
			;
		}
	}
}
