using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Queries;

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

		public static class Queries
		{
			public static QueryTemplate<EdgeType> Get = EdgeObjectsUtility.EntitySpace.CreateQueryTemplate<EdgeType>(Mappings.Default)
				.RootSubquery("select * from EdgeType where AccountID in (-1, @accountID) and ChannelID in (-1, @channelID)", init => init
					.DbParam("@accountID", query => query.Param<Account>("account") == null ? -1 : query.Param<Account>("account").ID)
					.DbParam("@channelID", query => query.Param<Channel>("channel") == null ? -1 : query.Param<Channel>("channel").ID)
				)
				.Param<Account>("account", required: false)
				.Param<Channel>("channel", required: false)
			;
		}

		public static IEnumerable<EdgeType> Get(Account account = null, Channel channel = null, PersistenceConnection connection = null)
		{
			return Queries.Get.Start()
				.Param<Account>("account", account)
				.Param<Channel>("channel", channel)
				.Connect(connection)
				.Execute(QueryExecutionMode.Buffered);
		}
	}
}
