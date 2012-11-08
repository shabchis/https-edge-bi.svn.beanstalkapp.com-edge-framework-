using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities;
using Eggplant.Entities.Model;


namespace Edge.Data.Objects
{
	public partial class EdgeObject
	{
		public static EntityDefinition<EdgeObject> Definition = new EntityDefinition<EdgeObject>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<EdgeObject, long> GK = new ValueProperty<EdgeObject, long>("GK");
			public static ValueProperty<EdgeObject, String> Name = new ValueProperty<EdgeObject, String>("Name");
			public static ReferenceProperty<EdgeObject, Account> Account = new ReferenceProperty<EdgeObject, Account>("Account");

			public static ValueProperty<EdgeObject, Dictionary<ConnectionDefinition, object>> Connections = new ValueProperty<EdgeObject, Dictionary<ConnectionDefinition, object>>("Connections");
			//public static DictionaryProperty<EdgeObject, ConnectionDefinition, Object> Connections = new DictionaryProperty<EdgeObject, ConnectionDefinition, Object>("Connections")
			//{
			//    Key = new ReferenceProperty<EdgeObject, ConnectionDefinition>(null),
			//    Value = new ValueProperty<EdgeObject, Object>(null)
			//};
		}
	}
}