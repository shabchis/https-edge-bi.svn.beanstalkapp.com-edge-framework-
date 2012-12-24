using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class Creative
	{
		public new static class Mappings
		{
			public static Mapping<Creative> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<Creative>(creative => creative
				.Inherit(EdgeObject.Mappings.Default)
			);
		}
	}
}
