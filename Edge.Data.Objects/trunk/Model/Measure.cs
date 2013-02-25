using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class Measure
	{
		public static EntityDefinition<Measure> Definition = new EntityDefinition<Measure>(fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<Measure, int> ID = new EntityProperty<Measure, int>("ID");
			public static EntityProperty<Measure, Account> Account = new EntityProperty<Measure, Account>("Account");
			public static EntityProperty<Measure, Channel> Channel = new EntityProperty<Measure, Channel>("Channel");
			public static EntityProperty<Measure, string> Name = new EntityProperty<Measure, string>("Name");
			public static EntityProperty<Measure, string> DisplayName = new EntityProperty<Measure, string>("DisplayName");
			public static EntityProperty<Measure, MeasureDataType> DataType = new EntityProperty<Measure, MeasureDataType>("DataType");
			public static EntityProperty<Measure, string> StringFormat = new EntityProperty<Measure, string>("StringFormat");
			public static EntityProperty<Measure, MeasureOptions> Options = new EntityProperty<Measure, MeasureOptions>("Options");
			//public static EntityProperty<Measure, bool> InheritedOptionsOverride = new EntityProperty<Measure, bool>("InheritedOptionsOverride");
			public static EntityProperty<Measure, bool> InheritedByDefault = new EntityProperty<Measure, bool>("InheritedByDefault");
		}
	}
}