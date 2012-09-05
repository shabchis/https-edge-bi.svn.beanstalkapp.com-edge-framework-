using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Account
	{
		public static EntityDefinition<Account> Definition = new EntityDefinition<Account>(fromReflection: typeof(Properties))
		{
			Identity = ac => ac.ID
		};

		public static class Properties
		{
			public static ValueProperty<Account, int> ID = new ValueProperty<Account, int>("ID")
			{
				AccessMode = AccessMode.WriteWhenDetached,
				DefaultValue = -1,
				EmptyValue = -1,
				AllowEmpty = false
			};

			public static ValueProperty<Account, string> Name = new ValueProperty<Account, string>("Name")
			{
				AccessMode = AccessMode.WriteAlways,
				AllowEmpty = false
			};


			public static ReferenceProperty<Account, Account> ParentAccount = new ReferenceProperty<Account, Account>("ParentAccount")
			{
				AccessMode = AccessMode.WriteAlways,
				AllowEmpty = true
			};
		}
	}
}
