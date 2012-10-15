using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Campaign
	{
		public static EntityDefinition<Campaign> Definition = new EntityDefinition<Campaign>(baseDefinition: ChannelSpecificObject.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<Campaign, Double> Budget = new ValueProperty<Campaign, Double>("Budget");
		}
	}
}