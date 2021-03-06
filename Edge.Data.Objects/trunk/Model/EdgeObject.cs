﻿using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class EdgeObject
	{
		public static EntityDefinition<EdgeObject> Definition = new EntityDefinition<EdgeObject>(fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<EdgeObject, long> GK = new EntityProperty<EdgeObject, long>("GK");
			public static EntityProperty<EdgeObject, string> TK = new EntityProperty<EdgeObject, string>("TK");
			public static EntityProperty<EdgeObject, Account> Account = new EntityProperty<EdgeObject, Account>("Account");
			public static EntityProperty<EdgeObject, EdgeType> EdgeType = new EntityProperty<EdgeObject, EdgeType>("EdgeType");
			public static EntityProperty<EdgeObject, Dictionary<Edge.Data.Objects.EdgeField, object>> Fields = new EntityProperty<EdgeObject, Dictionary<Edge.Data.Objects.EdgeField, object>>("Fields");
		}
	}
}