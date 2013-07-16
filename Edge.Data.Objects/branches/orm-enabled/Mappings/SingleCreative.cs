using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class SingleCreative
	{
		public new static class Mappings
		{
			public static Mapping<SingleCreative> Default = EdgeUtility.EntitySpace.CreateMapping<SingleCreative>(creative => creative
				.Inherit(Creative.Mappings.Default)
			);
		}
	}
}
