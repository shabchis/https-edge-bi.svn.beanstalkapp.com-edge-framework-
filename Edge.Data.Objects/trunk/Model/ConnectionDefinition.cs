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
			public static EntityProperty<ConnectionDefinition, int> ID = new EntityProperty<ConnectionDefinition, int>("ID");
			public static EntityProperty<ConnectionDefinition, string> Name = new EntityProperty<ConnectionDefinition, string>("Name");
			public static EntityProperty<ConnectionDefinition, Account> Account = new EntityProperty<ConnectionDefinition, Account>("Account");
			public static EntityProperty<ConnectionDefinition, Channel> Channel = new EntityProperty<ConnectionDefinition, Channel>("Channel");
			public static EntityProperty<ConnectionDefinition, EdgeType> EdgeType = new EntityProperty<ConnectionDefinition, EdgeType>("EdgeType");
			//public static EntityProperty<ConnectionDefinition, ConnectionOptions> Options = new EntityProperty<ConnectionDefinition, ConnectionOptions>("Options");
		}
	}
}