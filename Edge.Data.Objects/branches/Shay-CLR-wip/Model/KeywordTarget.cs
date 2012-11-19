﻿using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class KeywordTarget
	{
		public static EntityDefinition<KeywordTarget> Definition = new EntityDefinition<KeywordTarget>(baseDefinition: Target.Definition, fromReflection: typeof(Properties));

		public static class Properties
		{
			public static ValueProperty<KeywordTarget, KeywordMatchType> MatchType = new ValueProperty<KeywordTarget, KeywordMatchType>("MatchType");
		}
	}
}