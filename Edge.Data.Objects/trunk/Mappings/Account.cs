using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Queries;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Account
	{
		public static class Mappings
		{
			public static Mapping<Account> Default = EdgeUtility.EntitySpace.CreateMapping<Account>(account => account
				.Identity(Account.Identities.Default) // TODO: move this to query
				.Map<int>(Account.Properties.ID, "ID")
				.Map<string>(Account.Properties.Name, "Name")
				.Map<AccountStatus>(Account.Properties.Status, "Status")
				.Map<Account>(Account.Properties.ParentAccount, parentAccount => parentAccount
					.Do(context => context.NullIf<object>("ParentAccountID", parentAccountID => parentAccountID == null))
					.Identity(Account.Identities.Default)
					.Map<int>(Account.Properties.ID, "ParentAccountID")
				)
			);
		}

		public static class Identities
		{
			public static IdentityDefinition Default = new IdentityDefinition(Account.Properties.ID);
		}

		public static class Queries
		{
			public static QueryTemplate<Account> Get = EdgeUtility.EntitySpace.CreateQueryTemplate<Account>(Mappings.Default)
				.RootSubquery(EdgeUtility.GetSql<Account>("Get"), init => init
					.PersistenceParam("@accountID", fromQueryParam: "accountID")
				)
				.Param<int>("accountID", required: false)
			;

			public static QueryTemplate<Nothing> Save = EdgeUtility.EntitySpace.CreateQueryTemplate<Nothing>()
				.RootSubquery(EdgeUtility.GetSql<Account>("Save"), init => init
					.PersistenceParamMap(Account.Mappings.Default, "account")
				)
				.Param<Account>("account", required: true)
			;
		}

		public static IEnumerable<Account> Get(int accountID = -1, bool flat = false, PersistenceConnection connection = null)
		{
			var results = Queries.Get.Start()
				.Param<int>("accountID", accountID)
				.Connect(connection)
				.Execute();

			if (flat)
				return results;
			else
				return results.Where(account => account.ParentAccount == null);
		}

		public static void Save(Account account, PersistenceConnection connection = null)
		{
			Queries.Save.Start()
				.Param<Account>("account", account)
				.Connect(connection)
				.Execute();
		}

		public static void Save(IEnumerable<Account> accounts, PersistenceConnection connection = null)
		{
			Queries.Save.Start()
				.Batch<Account>(accounts, "account")
				.Connect(connection)
				.Execute();
		}
	}
}
