using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Measure
	{
		public static EntityDefinition<Measure> Definition = new EntityDefinition<Measure>(fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<Measure, Int32> ID = new ValueProperty<Measure, Int32>("ID");
			public static ValueProperty<Measure, String> Name = new ValueProperty<Measure, String>("Name");
			public static ValueProperty<Measure, String> DisplayName = new ValueProperty<Measure, String>("DisplayName");
			public static ReferenceProperty<Measure, Account> Account = new ReferenceProperty<Measure, Account>("Account");
			public static ReferenceProperty<Measure, Channel> Channel = new ReferenceProperty<Measure, Channel>("Channel");
			public static ReferenceProperty<Measure, Measure> BaseMeasure = new ReferenceProperty<Measure, Measure>("BaseMeasure");
			public static ValueProperty<Measure, String> StringFormat = new ValueProperty<Measure, String>("StringFormat");
			public static ValueProperty<Measure, MeasureDataType> DataType = new ValueProperty<Measure, MeasureDataType>("DataType");
			public static ValueProperty<Measure, MeasureOptions> Options = new ValueProperty<Measure, MeasureOptions>("Options");
		}
	}
}