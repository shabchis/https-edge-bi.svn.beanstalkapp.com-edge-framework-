using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class KeywordTarget
	{
		public new static class Mappings
		{
			public static Mapping<KeywordTarget> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<KeywordTarget>()
				.Inherit(Target.Mappings.Default)
				.Map<KeywordMatchType>(KeywordTarget.Properties.MatchType, "int_Field1")
			;
		}
	}
}
