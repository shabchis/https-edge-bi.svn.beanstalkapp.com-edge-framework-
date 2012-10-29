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
				.Scalar<int>(Measure.Properties.ID, "ID")
				.Scalar<string>(Measure.Properties.Name, "Name")
				.Scalar<string>(Measure.Properties.DisplayName, "DisplayName")
				.Scalar<Account>(Measure.Properties.Account, account => account
					.Scalar<int>(Account.Properties.ID, "AccountID")
					)
				.Scalar<Channel>(Measure.Properties.Channel, channel => channel
					.Scalar<int>(Channel.Properties.ID, "ChannelID")
					)
				.Scalar<Measure>(Measure.Properties.BaseMeasure, measure => measure
					.Scalar<int>(Measure.Properties.ID, "BaseMeasureID")
					)
				.Scalar<string>(Measure.Properties.StringFormat, "StringFormat")
				.Scalar<MeasureDataType>(Measure.Properties.DataType, "DataType")
				.Scalar<MeasureOptions>(Measure.Properties.Options, "Options")
			;
		}

		public static class Queries
		{
			//public Query<Measure> GetByName = new Query<Measure>()
			public static QueryTemplate<Measure> Get = EdgeObjectsUtility.EntitySpace.CreateQueryTemplate<Measure>(Mappings.Default)
				.Subquery(
					"Measure",
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
						.SetTopLevel(true)
					)
				.Param<Account>("account", required: false)
				.Param<Channel>("channel", required: false)
			;
		}

		public static IEnumerable<Measure> Get(PersistenceConnection connection, Account account = null, Channel channel = null)
		{			
			return Measure.Queries.Get.Start(connection)
				.Select(
					Measure.Properties.Name,
					Measure.Properties.DisplayName
					)
				.Filter(Measure.Properties.DataType, " = ", MeasureDataType.Currency)
				.Param<Account>("account", account)
				.Param<Channel>("channel", channel)
				.Execute(QueryExecutionMode.Buffered);
		}
	}
}
