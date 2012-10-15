using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class GenericMetricsUnit
	{
		public static EntityDefinition<GenericMetricsUnit> Definition = new EntityDefinition<GenericMetricsUnit>(baseDefinition: MetricsUnit.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ReferenceProperty<GenericMetricsUnit, Channel> Channel = new ReferenceProperty<GenericMetricsUnit, Channel>("Channel");
			public static ReferenceProperty<GenericMetricsUnit, Account> Account = new ReferenceProperty<GenericMetricsUnit, Account>("Account");
			public static DictionaryProperty<GenericMetricsUnit, MetaProperty, Object> PropertyDimensions = new DictionaryProperty<GenericMetricsUnit, MetaProperty, Object>("PropertyDimensions")
			{
				Key = new ReferenceProperty<GenericMetricsUnit, MetaProperty>(null),
				Value = new ValueProperty<GenericMetricsUnit, Object>(null)
			};
		}
	}
}