using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class CompositePartField
	{
		public static class Mappings
		{
			public static Mapping<CompositePartField> Default = EdgeUtility.EntitySpace.CreateMapping<CompositePartField>()
				.Inherit(EdgeField.Mappings.Default)
				.Map<Channel>(CompositePartField.Properties.Channel, channel => channel
					.Do(context => context.NullIf<int>("ChannelID", id => id == -1))
					.Map<int>(Channel.Properties.ID, "ChannelID")
				)
			;

		}
	}
}
