using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class EdgeCurrency
	{
		public static EntityDefinition<EdgeCurrency> Definition = new EntityDefinition<EdgeCurrency>(fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<EdgeCurrency, string> Code = new EntityProperty<EdgeCurrency, string>("Code");
		}
	}
}