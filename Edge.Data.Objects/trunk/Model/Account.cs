using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Account
	{
		public static EntityDefinition<Account> Definition = new EntityDefinition<Account>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<Account, int> ID = new EntityProperty<Account, int>("ID");
			public static EntityProperty<Account, string> Name = new EntityProperty<Account, string>("Name");
			public static EntityProperty<Account, Account> ParentAccount = new EntityProperty<Account, Account>("ParentAccount");
		}
	}
}