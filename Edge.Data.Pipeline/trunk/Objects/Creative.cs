using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects.Reflection;

namespace Edge.Data.Objects
{
	public abstract class Creative: MappedType
	{
		public string OriginalID;

		// Leave this null for now
		public string Name { get; set; }
	}

	[TypeID(1)]
	public class TextCreative : Creative
	{
		[FieldIndex(1)]
		public TextCreativeType TextType;


		[FieldIndex(2)]
		public string Text;
	}

	[TypeID(2)]
	public class ImageCreative : Creative
	{
		[FieldIndex(1)]
		public string ImageSize;

		[FieldIndex(2)]
		public string ImageUrl;
	}

	public enum TextCreativeType : int
	{
		Title = 1,
		Body = 2,
		DisplayUrl = 3
	}
}
