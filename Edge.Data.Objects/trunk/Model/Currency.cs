using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Currency
	{
		public static EntityDefinition<Currency> Definition = new EntityDefinition<Currency>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<Currency, String> Code = new ValueProperty<Currency, String>("Code");
		}
	}
}