using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class EdgeObject
	{
		public static EntityDefinition<EdgeObject> Definition = new EntityDefinition<EdgeObject>(fromReflection: typeof(Properties))
		{
			Identity = obj => obj.GK
		};

		public static class Properties
		{
			public static ValueProperty<EdgeObject, long> GK = new ValueProperty<EdgeObject, long>("GK")
			{
				AccessMode = AccessMode.ReadOnly,
				DefaultValue = -1,
				EmptyValue = -1,
				AllowEmpty = false
			};

			public static ValueProperty<EdgeObject, string> Name = new ValueProperty<EdgeObject, string>("Name")
			{
				AccessMode = AccessMode.WriteAlways, // is this right?
				AllowEmpty = false
			};

			public static ReferenceProperty<EdgeObject, Account> Account = new ReferenceProperty<EdgeObject, Account>("Account")
			{
				AccessMode = AccessMode.WriteWhenDetached,
				AllowEmpty = false
			};

			public static DictionaryProperty<EdgeObject, MetaProperty, object> MetaProperties = new DictionaryProperty<EdgeObject, MetaProperty, object>("MetaProperties")
			{
				Key = new ReferenceProperty<EdgeObject, MetaProperty>(null),
				Value = new ValueProperty<EdgeObject, object>(null)
			};
		}

	}
}
