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
			public static Mapping<Account> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<Account>(account => account
				.Identity(Account.Identities.Default)
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
			public static QueryTemplate<Account> Get = EdgeObjectsUtility.EntitySpace.CreateQueryTemplate<Account>(Mappings.Default)
				.RootSubquery(@"
					select *
					from Account 
					where @accountID = -1 or ID = @accountID or ParentAccountID = @accountID
					", init => init
						 .DbParam("@accountID", query => query.Param<int>("accountID"))
				)
				.Param<int>("accountID", required: false)
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
	}
}
