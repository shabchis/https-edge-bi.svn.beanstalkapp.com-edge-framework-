using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class ExtraField
	{
		public static EntityDefinition<ExtraField> Definition = new EntityDefinition<ExtraField>(baseDefinition: EdgeField.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<EdgeField, Account> Account = new EntityProperty<EdgeField, Account>("Account");
			public static EntityProperty<EdgeField, Channel> Channel = new EntityProperty<EdgeField, Channel>("Channel");
		}
	}
}