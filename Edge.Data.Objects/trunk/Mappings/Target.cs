using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class Target
	{
		public new static class Mappings
		{
			public static Mapping<Target> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<Target>()
				.Inherit(EdgeObject.Mappings.Default)
			;
		}
	}

}
