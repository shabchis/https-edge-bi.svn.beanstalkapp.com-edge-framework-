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
			public static EntityProperty<EdgeField, string> Name = new EntityProperty<EdgeField, string>("Name");
			public static EntityProperty<EdgeField, string> DisplayName = new EntityProperty<EdgeField, string>("DisplayName");
			public static EntityProperty<EdgeField, EdgeType> FieldEdgeType = new EntityProperty<EdgeField, EdgeType>("FieldEdgeType");
			public static EntityProperty<EdgeField, EdgeType> ParentEdgeType = new EntityProperty<EdgeField, EdgeType>("ParentEdgeType");
			public static EntityProperty<EdgeField, string> ColumnPrefix = new EntityProperty<EdgeField, string>("ColumnPrefix");
			public static EntityProperty<EdgeField, int> ColumnIndex = new EntityProperty<EdgeField, int>("ColumnIndex");
		}

		public static class Identities
		{
			public static IdentityDefinition Default = new IdentityDefinition(Properties.FieldID);	
		}
	}
}