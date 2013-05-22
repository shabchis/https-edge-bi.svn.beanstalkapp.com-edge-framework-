using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CurrencyRate
	{
		public static EntityDefinition<CurrencyRate> Definition = new EntityDefinition<CurrencyRate>(fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<CurrencyRate, EdgeCurrency> Currency = new EntityProperty<CurrencyRate, EdgeCurrency>("Currency");
			public static EntityProperty<CurrencyRate, DateTime> RateDate = new EntityProperty<CurrencyRate, DateTime>("RateDate");
			public static EntityProperty<CurrencyRate, double> RateValue = new EntityProperty<CurrencyRate, double>("RateValue");
			public static EntityProperty<CurrencyRate, DateTime> DateCreated = new EntityProperty<CurrencyRate, DateTime>("DateCreated");
		}
	}
}