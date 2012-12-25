using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class EdgeType
	{
		public static class Mappings
		{
			public static Mapping<EdgeType> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<EdgeType>(edgeType => edgeType
				.Map<int>(EdgeType.Properties.TypeID, "TypeID")
				.Map<string>(EdgeType.Properties.Name, "Name")
				.Map<Type>(EdgeType.Properties.ClrType, clrType => clrType
					.Set(context => Type.GetType(context.GetField<string>("ClrType")))
				)
				.Map<string>(EdgeType.Properties.TableName, "TableName")
				.Map<Account>(EdgeType.Properties.Account, account => account
					.Do(context => context.BreakIfNegative("AccountID"))
					.Map<int>(Account.Properties.ID, "AccountID")
				)
				.Map<Channel>(EdgeType.Properties.Channel, channel => channel
					.Do(context => context.BreakIfNegative("ChannelID"))
					.Map<int>(Channel.Properties.ID, "ChannelID")
				)
			);

		}
	}
}
