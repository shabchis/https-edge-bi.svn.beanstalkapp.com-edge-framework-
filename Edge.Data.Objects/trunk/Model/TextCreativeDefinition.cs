using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class TextCreativeDefinition
	{
		public static EntityDefinition<TextCreativeDefinition> Definition = new EntityDefinition<TextCreativeDefinition>(baseDefinition: SingleCreativeDefinition.Definition, fromReflection: true);

		public static class Properties
		{
		}
	}
}