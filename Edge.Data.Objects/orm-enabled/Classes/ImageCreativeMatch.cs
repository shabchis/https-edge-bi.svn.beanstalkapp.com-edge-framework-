using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class ImageCreativeMatch : SingleCreativeMatch
	{
		public string ImageSize;

		protected override Type CreativeType
		{
			get { return typeof(ImageCreative); }
		}

		public new ImageCreative Creative
		{
			get { return base.Creative as ImageCreative; }
			//set { base.Creative = value; }
		}
	}
}
