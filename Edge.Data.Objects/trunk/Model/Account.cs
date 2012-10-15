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
			public static ValueProperty<Account, Int32> ID = new ValueProperty<Account, Int32>("ID");
			public static ValueProperty<Account, String> Name = new ValueProperty<Account, String>("Name");
			public static ReferenceProperty<Account, Account> ParentAccount = new ReferenceProperty<Account, Account>("ParentAccount");
		}
	}
}