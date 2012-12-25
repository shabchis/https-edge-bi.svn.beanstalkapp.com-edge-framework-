using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class Channel
	{
		public static class Mappings
		{
			public static Mapping<Channel> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<Channel>(account => account
				.Map<int>(Channel.Properties.ID, "ID")
				.Map<string>(Channel.Properties.Name, "Name")
				.Map<ChannelType>(Channel.Properties.ChannelType, "ChannelType")
			);
		}
	}
}
