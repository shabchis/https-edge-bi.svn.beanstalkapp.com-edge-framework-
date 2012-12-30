using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class EdgeField
	{
		public static EntityDefinition<EdgeField> Definition = new EntityDefinition<EdgeField>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<EdgeField, int> FieldID = new EntityProperty<EdgeField, int>("FieldID");
			public static EntityProperty<EdgeField, bool> IsSystem = new EntityProperty<EdgeField, bool>("IsSystem");
			public static EntityProperty<EdgeField, Account> Account = new EntityProperty<EdgeField, Account>("Account");
			public static EntityProperty<EdgeField, Channel> Channel = new EntityProperty<EdgeField, Channel>("Channel");
			public static EntityProperty<EdgeField, string> Name = new EntityProperty<EdgeField, string>("Name");
			public static EntityProperty<EdgeField, string> DisplayName = new EntityProperty<EdgeField, string>("DisplayName");
			public static EntityProperty<EdgeField, EdgeType> ObjectEdgeType = new EntityProperty<EdgeField, EdgeType>("ObjectEdgeType");
			public static EntityProperty<EdgeField, EdgeType> FieldEdgeType = new EntityProperty<EdgeField, EdgeType>("FieldEdgeType");
			public static EntityProperty<EdgeField, Type> FieldClrType = new EntityProperty<EdgeField, Type>("FieldClrType");
			public static EntityProperty<EdgeField, string> ColumnPrefix = new EntityProperty<EdgeField, string>("ColumnPrefix");
			public static EntityProperty<EdgeField, int> ColumnIndex = new EntityProperty<EdgeField, int>("ColumnIndex");
		}

		public static class Identities
		{
			public static IdentityDefinition Default = new IdentityDefinition(Properties.FieldID);
			public static IdentityDefinition Unique = new IdentityDefinition(Properties.IsSystem, Properties.Channel, Properties.ObjectEdgeType, Properties.Name);
		}
	}
}