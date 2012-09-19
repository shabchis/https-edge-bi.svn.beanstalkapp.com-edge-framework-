using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[TableInfo(Name="CreativeImage")]
	public partial class ImageCreative : SingleCreative
	{
		public string ImageUrl;
		public string ImageSize;
	}

}
