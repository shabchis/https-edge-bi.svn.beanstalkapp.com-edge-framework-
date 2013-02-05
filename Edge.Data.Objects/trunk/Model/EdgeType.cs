using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class EdgeType
	{
		public static EntityDefinition<EdgeType> Definition = new EntityDefinition<EdgeType>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<EdgeType, int> TypeID = new EntityProperty<EdgeType, int>("TypeID");
			public static EntityProperty<EdgeType, EdgeType> BaseEdgeType = new EntityProperty<EdgeType, EdgeType>("BaseEdgeType");
			public static EntityProperty<EdgeType, Type> ClrType = new EntityProperty<EdgeType, Type>("ClrType");
			public static EntityProperty<EdgeType, string> Name = new EntityProperty<EdgeType, string>("Name");
			public static EntityProperty<EdgeType, bool> IsAbstract = new EntityProperty<EdgeType, bool>("IsAbstract");
			public static EntityProperty<EdgeType, string> TableName = new EntityProperty<EdgeType, string>("TableName");
			public static EntityProperty<EdgeType, Account> Account = new EntityProperty<EdgeType, Account>("Account");
			public static EntityProperty<EdgeType, Channel> Channel = new EntityProperty<EdgeType, Channel>("Channel");
		}
	}
}