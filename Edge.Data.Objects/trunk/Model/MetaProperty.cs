using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class MetaProperty
	{
		public static EntityDefinition<MetaProperty> Definition = new EntityDefinition<MetaProperty>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<MetaProperty, Int32> ID = new ValueProperty<MetaProperty, Int32>("ID");
			public static ValueProperty<MetaProperty, String> PropertyName = new ValueProperty<MetaProperty, String>("PropertyName");
			public static ReferenceProperty<MetaProperty, Account> Account = new ReferenceProperty<MetaProperty, Account>("Account");
			public static ReferenceProperty<MetaProperty, Channel> Channel = new ReferenceProperty<MetaProperty, Channel>("Channel");
			public static ValueProperty<MetaProperty, Type> BaseValueType = new ValueProperty<MetaProperty, Type>("BaseValueType");
			public static ValueProperty<MetaProperty, MetaPropertyOptions> Options = new ValueProperty<MetaProperty, MetaPropertyOptions>("Options");
		}
	}
}