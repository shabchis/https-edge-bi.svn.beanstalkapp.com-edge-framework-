using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class ExtraField
	{
		public static class Mappings
		{
			public static Mapping<ExtraField> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<ExtraField>()
				.Inherit(EdgeField.Mappings.Default)
				.Map<Account>(ExtraField.Properties.Account, account => account
					.Do(context => context.NullIf<int>("AccountID", id => id == -1))
					.Map<int>(Account.Properties.ID, "AccountID")
				)
				.Map<Channel>(ExtraField.Properties.Channel, channel => channel
					.Do(context => context.NullIf<int>("ChannelID", id => id == -1))
					.Map<int>(Channel.Properties.ID, "ChannelID")
				)
			;

		}
	}
}
