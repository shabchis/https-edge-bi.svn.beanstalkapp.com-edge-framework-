using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Currency
	{
		public static EntityDefinition<Currency> Definition = new EntityDefinition<Currency>(fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<Currency, string> Code = new EntityProperty<Currency, string>("Code");
		}
	}
}