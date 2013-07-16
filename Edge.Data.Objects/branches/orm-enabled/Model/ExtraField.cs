using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class ExtraField
	{
		public static EntityDefinition<ExtraField> Definition = new EntityDefinition<ExtraField>(baseDefinition: EdgeField.Definition, fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<ExtraField, Account> Account = new EntityProperty<ExtraField, Account>("Account");
			public static EntityProperty<ExtraField, Channel> Channel = new EntityProperty<ExtraField, Channel>("Channel");
		}
	}
}