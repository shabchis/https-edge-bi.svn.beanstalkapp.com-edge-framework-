using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class EdgeTypeField
	{
		public static EntityDefinition<EdgeTypeField> Definition = new EntityDefinition<EdgeTypeField>(fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<EdgeTypeField, EdgeField> Field = new EntityProperty<EdgeTypeField, EdgeField>("Field");
			public static EntityProperty<EdgeTypeField, string> ColumnName = new EntityProperty<EdgeTypeField, string>("ColumnName");
			public static EntityProperty<EdgeTypeField, bool> IsIdentity = new EntityProperty<EdgeTypeField, bool>("IsIdentity");
		}
	}
}