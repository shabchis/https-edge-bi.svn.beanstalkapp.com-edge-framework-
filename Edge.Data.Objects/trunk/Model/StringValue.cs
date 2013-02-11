using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class StringValue
	{
		public static EntityDefinition<StringValue> Definition = new EntityDefinition<StringValue>(baseDefinition: ChannelSpecificObject.Definition, fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<StringValue, string> Value = new EntityProperty<StringValue, string>("Value");
		}
	}
}