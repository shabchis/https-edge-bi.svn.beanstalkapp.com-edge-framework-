using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract class Creative
	{
		public string OriginalID;

		// Leave this null for now
		public string Name { get; private set; }
	}

	[CreativeTypeID(1)]
	public class TextCreative : Creative
	{
		[CreativeFieldIndex(1)]
		public TextCreativeType TextType;

		[CreativeFieldIndex(2)]
		public string Text;
	}

	[CreativeTypeID(2)]
	public class ImageCreative : Creative
	{
		[CreativeFieldIndex(1)]
		public string ImageSize;

		[CreativeFieldIndex(2)]
		public string ImageUrl;
	}

	public enum TextCreativeType
	{
		Title = 1,
		Body = 2,
		DisplayUrl = 3
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
