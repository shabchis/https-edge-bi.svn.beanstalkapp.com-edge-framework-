using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CompositeCreative
	{
		public static EntityDefinition<CompositeCreative> Definition = new EntityDefinition<CompositeCreative>(baseDefinition: Creative.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static DictionaryProperty<CompositeCreative, String, SingleCreative> ChildCreatives = new DictionaryProperty<CompositeCreative, String, SingleCreative>("ChildCreatives")
			{
				Key = new ValueProperty<CompositeCreative, String>(null),
				Value = new ReferenceProperty<CompositeCreative, SingleCreative>(null)
			};
		}
	}
}