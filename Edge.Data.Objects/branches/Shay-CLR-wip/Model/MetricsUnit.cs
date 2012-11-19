using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class MetricsUnit
	{
		public static EntityDefinition<MetricsUnit> Definition = new EntityDefinition<MetricsUnit>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<MetricsUnit, DateTime> TimePeriodStart = new ValueProperty<MetricsUnit, DateTime>("TimePeriodStart");
			public static ValueProperty<MetricsUnit, DateTime> TimePeriodEnd = new ValueProperty<MetricsUnit, DateTime>("TimePeriodEnd");
			public static ReferenceProperty<MetricsUnit, Currency> Currency = new ReferenceProperty<MetricsUnit, Currency>("Currency");
			public static CollectionProperty<MetricsUnit, TargetMatch> TargetDimensions = new CollectionProperty<MetricsUnit, TargetMatch>("TargetDimensions")
			{
				Value = new ReferenceProperty<MetricsUnit, TargetMatch>(null)
			};
			public static DictionaryProperty<MetricsUnit, Measure, Double> MeasureValues = new DictionaryProperty<MetricsUnit, Measure, Double>("MeasureValues")
			{
				Key = new ReferenceProperty<MetricsUnit, Measure>(null),
				Value = new ValueProperty<MetricsUnit, Double>(null)
			};
		}
	}
}