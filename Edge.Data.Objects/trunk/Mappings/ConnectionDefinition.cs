using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class ConnectionDefinition
	{
		public static class Mappings
		{
			public static Mapping<ConnectionDefinition> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<ConnectionDefinition>(connectionDef => connectionDef
				.Map<int>(ConnectionDefinition.Properties.ID, "ID")
				.Map<string>(ConnectionDefinition.Properties.Name, "Name")
				.Map<Account>(ConnectionDefinition.Properties.Account, account => account
					.Do(context => context.BreakIfNegative("AccountID"))
					.Map<int>(Account.Properties.ID, "AccountID")
				)
				.Map<Channel>(ConnectionDefinition.Properties.Channel, channel => channel
					.Do(context => context.BreakIfNegative("ChannelID"))
					.Map<int>(Channel.Properties.ID, "ChannelID")
				)
				.Map<EdgeType>(ConnectionDefinition.Properties.ToEdgeType, edgeType => edgeType
					.Map<int>(Account.Properties.ID, "ToTypeID")
				)
			);
		}
	}
}
