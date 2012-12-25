using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class Account
	{
		public static class Mappings
		{
			public static Mapping<Account> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<Account>(account => account
				.Map<int>(Account.Properties.ID, "ID")
				.Map<string>(Account.Properties.Name, "Name")
				.Map<AccountStatus>(Account.Properties.Status, "Status")
				.Map<Account>(Account.Properties.ParentAccount, parentAccount => parentAccount
					.Map<int>(Account.Properties.ID, "ParentAccountID")
				)
			);
		}
	}
}
