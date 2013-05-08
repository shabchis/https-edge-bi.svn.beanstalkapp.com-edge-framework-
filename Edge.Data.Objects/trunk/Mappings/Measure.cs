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
			public static Mapping<Measure> Default = EdgeUtility.EntitySpace.CreateMapping<Measure>()
				.Map<int>(Measure.Properties.ID, "ID")
				.Map<Account>(Measure.Properties.Account, account => account
					.Map<int>(Account.Properties.ID, "AccountID")
					)
				.Map<string>(Measure.Properties.Name, "Name")
				.Map<MeasureDataType>(Measure.Properties.DataType, "DataType")
				.Map<string>(Measure.Properties.DisplayName, "DisplayName")
				.Map<string>(Measure.Properties.StringFormat, "StringFormat")
				.Map<MeasureOptions>(Measure.Properties.Options, "Options")
				.Map<bool>(Measure.Properties.OptionsOverride, "OptionsOverride")
				.Map<bool>(Measure.Properties.IsInstance, "IsInstance")
			;
		}

		public static class Identities
		{
			public static IdentityDefinition ByID = new IdentityDefinition(Measure.Properties.ID)
				.Constrain<bool>(Measure.Properties.IsInstance, val => val == false);
			public static IdentityDefinition ByName = new IdentityDefinition(Measure.Properties.Account, Measure.Properties.Name)
				.Constrain<bool>(Measure.Properties.IsInstance, val => val == false);
		}

		public static class Queries
		{
			public static QueryTemplate<Measure> GetInstances = EdgeUtility.EntitySpace.CreateQueryTemplate<Measure>(
				EdgeUtility.EntitySpace.CreateMapping<Measure>()
					.Identity(Identities.ByName)
					.UseMapping(Measure.Mappings.Default)
				)
				.RootSubquery(EdgeUtility.GetSql<Measure>("GetInstances"), subquery => subquery
					.PersistenceParam("@accountID", fromQueryParam: "account", convertQueryParam: EdgeUtility.ConvertAccountToID)
					.PersistenceParam("@operator", fromQueryParam: "options", convertQueryParam:
						options => options == null ? FlagsOperator.ContainsAny : ((FlagsQuery?)options).Value.Operator
					)
					.PersistenceParam("@flags", fromQueryParam: "options", convertQueryParam:
						options => options == null ? 0 : ((FlagsQuery?)options).Value.Value
					)
				)
				.Param<Account>("account", required: false)
				.Param<FlagsQuery?>("options", required: false)
			;
		}

		public static IEnumerable<Measure> GetInstances(Account account = null, FlagsQuery? options = null, PersistenceConnection connection = null)
		{
			return Measure.Queries.GetInstances.Start()
				.Param<Account>("account", account)
				.Param<FlagsQuery?>("options", options)
				.Execute();
		}

		public static IEnumerable<Measure> GetInstances(int accountID, FlagsQuery? options = null, PersistenceConnection connection = null)
		{
			return GetInstances(new Account() { ID = accountID }, options, connection);
		}
	}
}
