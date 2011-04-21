using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	public class Creative
	{
		public string OriginalID;
		public string Name;
	}

	[CreativeType(1)]
	public class TextCreative : Creative
	{
		[CreativeColumn(1)]
		public string Text;
		[CreativeColumn(2)]
		public TextCreativeType TextType;
	}

	[CreativeType(2)]
	public class ImageCreative : Creative
	{
		[CreativeColumn(1)]
		public string ImageUrl;
		[CreativeColumn(2)]
		public string ImageSize;
	}

	public enum TextCreativeType
	{
		Title,
		Body,
		DisplayUrl
	}
}
