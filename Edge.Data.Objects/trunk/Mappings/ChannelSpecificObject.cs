using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Queries;

namespace Edge.Data.Objects
{
	public partial class ChannelSpecificObject
	{
		public new static class Mappings
		{
			public static Mapping<ChannelSpecificObject> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<ChannelSpecificObject>(edgeObject => edgeObject

				.Inherit(EdgeObject.Mappings.Default)

				.Map<string>(ChannelSpecificObject.Properties.OriginalID, "OriginalID")
				.Map<ObjectStatus>(ChannelSpecificObject.Properties.Status, "Status")
				.Map<Channel>(ChannelSpecificObject.Properties.Channel, channel => channel
					.Do(context => context.NullIf<int>("ChannelID", id => id == -1))
					.Map<int>(Account.Properties.ID, "ChannelID")
				)
			);
		}
	}
}
