using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class ConnectionDefinition
	{
		public static EntityDefinition<ConnectionDefinition> Definition = new EntityDefinition<ConnectionDefinition>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<ConnectionDefinition, Int32> ID = new ValueProperty<ConnectionDefinition, Int32>("ID");
			public static ValueProperty<ConnectionDefinition, String> ConnectionName = new ValueProperty<ConnectionDefinition, String>("ConnectionName");
			public static ReferenceProperty<ConnectionDefinition, Account> Account = new ReferenceProperty<ConnectionDefinition, Account>("Account");
			public static ReferenceProperty<ConnectionDefinition, Channel> Channel = new ReferenceProperty<ConnectionDefinition, Channel>("Channel");
			public static ValueProperty<ConnectionDefinition, Type> BaseValueType = new ValueProperty<ConnectionDefinition, Type>("BaseValueType");
			public static ValueProperty<ConnectionDefinition, ConnectionOptions> Options = new ValueProperty<ConnectionDefinition, ConnectionOptions>("Options");
		}
	}
}