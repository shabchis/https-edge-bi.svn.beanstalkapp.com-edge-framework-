using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class LandingPage
	{
		public static EntityDefinition<LandingPage> Definition = new EntityDefinition<LandingPage>(baseDefinition: EdgeObject.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<LandingPage, LandingPageType> LandingPageType = new ValueProperty<LandingPage, LandingPageType>("LandingPageType");
		}
	}
}