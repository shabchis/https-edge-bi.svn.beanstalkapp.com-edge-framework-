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
				.Identity(EdgeType.Identities.Default)
				.Map<int>(EdgeType.Properties.TypeID, "TypeID")
				.Map<EdgeType>(EdgeType.Properties.BaseEdgeType, baseEdgeType => baseEdgeType
					.Do(context=>context.NullIf<object>("BaseTypeID", id => id == null))
					.Map<int>(EdgeType.Properties.TypeID, "BaseTypeID")
				)
				.Map<Type>(EdgeType.Properties.ClrType, clrType => clrType
					.Set(context => Type.GetType(context.GetField<string>("ClrType")))
				)
				.Map<string>(EdgeType.Properties.Name, "Name")
				.Map<string>(EdgeType.Properties.TableName, "TableName")
				.Map<bool>(EdgeType.Properties.IsAbstract, "IsAbstract")
				.Map<Account>(EdgeType.Properties.Account, account => account
					.Do(context => context.NullIf<int>("AccountID", id => id == -1))
					.Map<int>(Account.Properties.ID, "AccountID")
				)
				.Map<Channel>(EdgeType.Properties.Channel, channel => channel
					.Do(context => context.NullIf<int>("ChannelID", id => id == -1))
					.Map<int>(Channel.Properties.ID, "ChannelID")
				)

				.MapListFromSubquery<EdgeType, EdgeField>(EdgeType.Properties.Fields, "EdgeFields",
					parent => parent
						.Identity(EdgeType.Identities.Default)
						.Map<int>(EdgeType.Properties.TypeID, "ParentTypeID")
					,
					item => item
						.UseMapping(EdgeField.Mappings.Default)
				)
			);

		}

		public static class Queries
		{
			public static QueryTemplate<EdgeType> Get = EdgeObjectsUtility.EntitySpace.CreateQueryTemplate<EdgeType>(Mappings.Default)
				.RootSubquery(@"
					select *
					from MD_EdgeType
					where
						AccountID in (-1, @accountID) and
						ChannelID in (-1, @channelID)
				", init => init
					.DbParam("@accountID", query => query.Param<Account>("account") == null ? -1 : query.Param<Account>("account").ID)
					.DbParam("@channelID", query => query.Param<Channel>("channel") == null ? -1 : query.Param<Channel>("channel").ID)
				)
				.Subquery("EdgeFields", @"
					select
						types.TypeID as ParentTypeID,
						fields.FieldID as FieldID,
						fields.FieldType as FieldType,
						fields.Name as Name
					from
						MD_EdgeType as types
						inner join MD_EdgeField as fields on
							fields.ParentTypeID in (-1, types.TypeID) and
							fields.AccountID in (-1, types.AccountID) and
							fields.ChannelID in (-1, types.ChannelID)
					where
						types.AccountID in (-1, @accountID) and
						types.ChannelID in (-1, @channelID)
				", init => init
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
				.Execute();
		}
	}
}
