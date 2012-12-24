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
			public static EntityProperty<LandingPage, LandingPageType> LandingPageType = new EntityProperty<LandingPage, LandingPageType>("LandingPageType");
		}
	}
}