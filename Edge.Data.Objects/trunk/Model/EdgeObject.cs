using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class EdgeObject
	{
		public static EntityDefinition<EdgeObject> Definition = new EntityDefinition<EdgeObject>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static EntityProperty<EdgeObject, long> GK = new EntityProperty<EdgeObject, long>("GK");
			public static EntityProperty<EdgeObject, Account> Account = new EntityProperty<EdgeObject, Account>("Account");
			public static EntityProperty<EdgeObject, EdgeType> EdgeType = new EntityProperty<EdgeObject, EdgeType>("EdgeType");
			public static EntityProperty<EdgeObject, Dictionary<ConnectionDefinition, EdgeObject>> Connections = new EntityProperty<EdgeObject, Dictionary<Edge.Data.Objects.ConnectionDefinition, Edge.Data.Objects.EdgeObject>>("Connections");
			public static EntityProperty<EdgeObject, Dictionary<EdgeField, object>> ExtraFields = new EntityProperty<EdgeObject, Dictionary<EdgeField, object>>("ExtraFields");
		}
	}
}