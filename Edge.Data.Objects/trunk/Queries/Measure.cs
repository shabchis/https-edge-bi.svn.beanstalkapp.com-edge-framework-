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
				.Set(context => new Measure())
				.Map<int>(Measure.Properties.ID, "ID")
				.Map<string>(Measure.Properties.Name, "Name")
				.Map<string>(Measure.Properties.DisplayName, "DisplayName")
				.Map<Account>(Measure.Properties.Account, account => account
					.Map<int>(Account.Properties.ID, "AccountID")
					)
				.Map<Channel>(Measure.Properties.Channel, channel => channel
					.Map<int>(Channel.Properties.ID, "ChannelID")
					)
				.Map<Measure>(Measure.Properties.BaseMeasure, measure => measure
					.Map<int>(Measure.Properties.ID, "BaseMeasureID")
					)
				.Map<string>(Measure.Properties.StringFormat, "StringFormat")
				.Map<MeasureDataType>(Measure.Properties.DataType, "DataType")
				.Map<MeasureOptions>(Measure.Properties.Options, "Options")
			;
		}

		public static class Queries
		{
			//public Query<Measure> GetByName = new Query<Measure>()
			public static QueryTemplate<Measure> Get = EdgeObjectsUtility.EntitySpace.CreateQueryTemplate<Measure>(Mappings.Default)
				.RootSubquery(
					EdgeObjectsUtility.GetEdgeTemplate("Measure.sql", "Measure.Queries.Get"),
					subquery => subquery
						.ConditionalColumn("ID", Measure.Properties.ID)
						.ConditionalColumn("Name", Measure.Properties.Name)
						.ConditionalColumn("DisplayName", Measure.Properties.DisplayName)
						.ConditionalColumn("AccountID", Measure.Properties.Account)
						.ConditionalColumn("ChannelID", Measure.Properties.Channel)
						.ConditionalColumn("StringFormat", Measure.Properties.StringFormat)
						.ConditionalColumn("DataType", Measure.Properties.DataType)
						.ConditionalColumn("Options", Measure.Properties.Options)
						.DbParam("@accountID", query => query.Param<Account>("account") == null ? -1 : query.Param<Account>("account").ID)
						.DbParam("@channelID", query => query.Param<Channel>("channel") == null ? -1 : query.Param<Channel>("channel").ID)
						.ParseEdgeTemplate()
					)
				.Param<Account>("account", required: false)
				.Param<Channel>("channel", required: false)
			;
		}

		public static IEnumerable<Measure> Get(Account account = null, Channel channel = null, PersistenceConnection connection = null)
		{			
			return Measure.Queries.Get.Start()
				.Select(
					Measure.Properties.Name,
					Measure.Properties.DisplayName
					)
				.Param<Account>("account", account)
				.Param<Channel>("channel", channel)
				.Filter(Measure.Properties.DataType, " = ", MeasureDataType.Currency)
				.Execute(QueryExecutionMode.Buffered);
		}
	}
}
