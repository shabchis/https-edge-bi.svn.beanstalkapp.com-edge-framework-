using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class AdMetricsUnit
	{
		public static EntityDefinition<AdMetricsUnit> Definition = new EntityDefinition<AdMetricsUnit>(baseDefinition: MetricsUnit.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ReferenceProperty<AdMetricsUnit, Ad> Ad = new ReferenceProperty<AdMetricsUnit, Ad>("Ad");
		}
	}
}