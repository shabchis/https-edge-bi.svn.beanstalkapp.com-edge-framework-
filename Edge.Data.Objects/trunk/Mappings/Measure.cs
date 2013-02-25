using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Queries;

namespace Edge.Data.Objects
{
	public partial class Measure
	{
		public static class Mappings
		{
			public static Mapping<Measure> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<Measure>()
				.Map<int>(Measure.Properties.ID, "ID")
				.Map<Account>(Measure.Properties.Account, account => account
					.Map<int>(Account.Properties.ID, "AccountID")
					)
				.Map<Channel>(Measure.Properties.Channel, channel => channel
					.Map<int>(Channel.Properties.ID, "ChannelID")
					)
				.Map<string>(Measure.Properties.Name, "Name")
				.Map<MeasureDataType>(Measure.Properties.DataType, "DataType")
				.Map<string>(Measure.Properties.DisplayName, "DisplayName")
				.Map<string>(Measure.Properties.StringFormat, "StringFormat")
				.Map<MeasureOptions>(Measure.Properties.Options, "Options")
				//.Map<bool>(Measure.Properties.InheritedOptionsOverride, "InheritedOptionsOverride")
				.Map<bool>(Measure.Properties.InheritedByDefault, "InheritedByDefault")
			;
		}

		public static class Identities
		{
			public static IdentityDefinition ByID = new IdentityDefinition(Measure.Properties.ID);
			public static IdentityDefinition ByName = new IdentityDefinition(Measure.Properties.Name);
		}

		public static class Queries
		{
			//public Query<Measure> GetByName = new Query<Measure>()
			public static QueryTemplate<Measure> Get = EdgeObjectsUtility.EntitySpace.CreateQueryTemplate<Measure>(Mappings.Default)
				.RootSubquery(@"
						select *
						from
						(
							select
								AccountID,
								ChannelID,
								Name,
								DataType,
								DisplayName,
								StringFormat,
								Options,
								InheritedOptionsOverride,
								InheritedByDefault
							from
								MD_Measure
							where
								AccountID in (@accountID, -1) and
								ChannelID in (@channelID, -1)
						) as temp
						where
							(AccountID != -1 and ChannelID != -1)
						
							
					", subquery => subquery
						.DbParam("@accountID", query => query.Param<Account>("account") == null ? -1 : query.Param<Account>("account").ID)
						.DbParam("@channelID", query => query.Param<Channel>("channel") == null ? -1 : query.Param<Channel>("channel").ID)
					)
				.Param<Account>("account", required: false)
				.Param<Channel>("channel", required: false)
			;
		}

		public static IEnumerable<Measure> Get(Account account = null, Channel channel = null, bool includeBase = false, PersistenceConnection connection = null)
		{			
			return Measure.Queries.Get.Start()
				.Select(
					Measure.Properties.Name,
					Measure.Properties.DisplayName
					)
				.Param<Account>("account", account)
				.Param<Channel>("channel", channel)
				.Execute();
		}
	}
}
