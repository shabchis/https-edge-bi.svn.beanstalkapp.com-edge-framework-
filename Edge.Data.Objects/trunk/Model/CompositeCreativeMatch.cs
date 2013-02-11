using System;
using System.Collections.Generic;
using System.Linq;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class CompositeCreativeMatch
	{
		public static EntityDefinition<CompositeCreativeMatch> Definition = new EntityDefinition<CompositeCreativeMatch>(baseDefinition: CreativeMatch.Definition, fromReflection: true);

		public static class Properties
		{
			public static EntityProperty<CompositeCreativeMatch, Dictionary<Edge.Data.Objects.CompositePartField, Edge.Data.Objects.SingleCreativeMatch>> CreativesMatches = new EntityProperty<CompositeCreativeMatch, Dictionary<Edge.Data.Objects.CompositePartField, Edge.Data.Objects.SingleCreativeMatch>>("CreativesMatches");
		}
	}
}