using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Queries;
using Eggplant.Entities.Persistence.SqlServer;
using System.Data;

namespace Edge.Data.Objects
{
	public partial class EdgeType
	{
		public static class Mappings
		{
			public static Mapping<EdgeType> Default = EdgeUtility.EntitySpace.CreateMapping<EdgeType>(edgeType => edgeType
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

				.MapListFromSubquery<EdgeType, EdgeTypeField>(EdgeType.Properties.Fields, "EdgeTypeFields",
					parent => parent
						.Identity(EdgeType.Identities.Default)
						.Map<int>(EdgeType.Properties.TypeID, "ParentTypeID")
					,
					item => item
						.Map<EdgeField>(EdgeTypeField.Properties.Field, field => field
							.UseMapping(EdgeField.Mappings.Default)
						)
						.Map<string>(EdgeTypeField.Properties.ColumnName, "ColumnName")
						.Map<bool>(EdgeTypeField.Properties.IsIdentity, "IsIdentity")
				)
			);

		}

		public static class Queries
		{
			public static QueryTemplate<EdgeType> Get = EdgeUtility.EntitySpace.CreateQueryTemplate<EdgeType>(Mappings.Default)
				.RootSubquery(EdgeUtility.GetPersistenceAction("EdgeType.sql", "Get"), init => init
					.PersistenceParam("@accountID", fromQueryParam: "account", convertQueryParam: EdgeUtility.ConvertAccountToID)
					.PersistenceParam("@channelID", fromQueryParam: "channel", convertQueryParam: EdgeUtility.ConvertChannelToID)
				)
				.Subquery("EdgeTypeFields", EdgeUtility.GetPersistenceAction("EdgeType.sql", "Get/EdgeTypeFields"), init => init
					.PersistenceParam("@accountID", fromQueryParam: "account", convertQueryParam: EdgeUtility.ConvertAccountToID)
					.PersistenceParam("@channelID", fromQueryParam: "channel", convertQueryParam: EdgeUtility.ConvertChannelToID)
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
