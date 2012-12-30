using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class Ad
	{
		public new static class Mappings
		{
			public static Mapping<Ad> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<Ad>(ad => ad
				.Inherit(ChannelSpecificObject.Mappings.Default)

				.Map<string>(Ad.Properties.DestinationUrl, "DestinationUrl")
				.Map<Creative>(Ad.Properties.Creative, creative => creative
					.MapEdgeObject("CreativeGK", "CreativeTypeID", "CreativeClrType")
				)
			);
		}
	}
}
