using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class GenderTarget
	{
		public new static class Mappings
		{
			public static Mapping<GenderTarget> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<GenderTarget>()
				.Inherit(Target.Mappings.Default)
				.Map<Gender>(GenderTarget.Properties.Gender, "int_Field1")
			;
		}
	}

}
