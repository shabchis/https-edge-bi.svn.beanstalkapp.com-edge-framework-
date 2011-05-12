using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract class Creative
	{
		public string OriginalID;
		public string Name { get; private set; }
	}

	[CreativeTypeID(1)]
	public class TextCreative : Creative
	{
		[CreativeFieldIndex(1)]
		public string Text;
		//[CreativeFieldIndex(2)]
		//public TextCreativeType TextType;
	}

	[CreativeTypeID(2)]
	public class ImageCreative : Creative
	{
		[CreativeFieldIndex(1)]
		public string ImageUrl;
		[CreativeFieldIndex(2)]
		public string ImageSize;
	}

	public enum TextCreativeType
	{
		Title,
		Body,
		DisplayUrl
	}

	#region Attributes

	class CreativeTypeIDAttribute : Attribute
	{
		internal int CreativeTypeID;
		public CreativeTypeIDAttribute(int creativeTypeID)
		{
			CreativeTypeID = creativeTypeID;
		}
	}

	class CreativeFieldIndexAttribute : Attribute
	{
		internal int CreativeFieldIndex;
		public CreativeFieldIndexAttribute(int creativeFieldIndex)
		{
			CreativeFieldIndex = creativeFieldIndex;
		}
	}

	#endregion
}
