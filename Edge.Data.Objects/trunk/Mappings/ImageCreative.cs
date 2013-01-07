using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class ImageCreative : SingleCreative
	{
		public new static class Mappings
		{
			public static Mapping<ImageCreative> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<ImageCreative>(creative => creative
				.Inherit(SingleCreative.Mappings.Default)

				//.Map<string>(ImageCreative.Properties.ImageUrl, "string_Field1")
				//.Map<string>(ImageCreative.Properties.ImageSize, "string_Field2")

				//.MapEdgeField<ImageCreative, string>(ImageCreative.Properties.ImageUrl)
				//.MapEdgeField<ImageCreative, string>(ImageCreative.Properties.ImageSize)
			);
		}
	}

}
