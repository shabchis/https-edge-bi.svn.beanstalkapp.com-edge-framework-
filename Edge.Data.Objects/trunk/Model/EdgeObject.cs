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
			public static ValueProperty<EdgeObject, UInt64> GK = new ValueProperty<EdgeObject, UInt64>("GK");
			public static ValueProperty<EdgeObject, String> Name = new ValueProperty<EdgeObject, String>("Name");
			public static ReferenceProperty<EdgeObject, Account> Account = new ReferenceProperty<EdgeObject, Account>("Account");
			public static DictionaryProperty<EdgeObject, ConnectionDefinition, Object> MetaProperties = new DictionaryProperty<EdgeObject, ConnectionDefinition, Object>("MetaProperties")
			{
				Key = new ReferenceProperty<EdgeObject, ConnectionDefinition>(null),
				Value = new ValueProperty<EdgeObject, Object>(null)
			};
		}
	}
}