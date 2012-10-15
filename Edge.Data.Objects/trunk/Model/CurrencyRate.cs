using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CurrencyRate
	{
		public static EntityDefinition<CurrencyRate> Definition = new EntityDefinition<CurrencyRate>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ReferenceProperty<CurrencyRate, Currency> Currency = new ReferenceProperty<CurrencyRate, Currency>("Currency");
			public static ValueProperty<CurrencyRate, DateTime> RateDate = new ValueProperty<CurrencyRate, DateTime>("RateDate");
			public static ValueProperty<CurrencyRate, Double> RateValue = new ValueProperty<CurrencyRate, Double>("RateValue");
			public static ValueProperty<CurrencyRate, DateTime> DateCreated = new ValueProperty<CurrencyRate, DateTime>("DateCreated");
		}
	}
}