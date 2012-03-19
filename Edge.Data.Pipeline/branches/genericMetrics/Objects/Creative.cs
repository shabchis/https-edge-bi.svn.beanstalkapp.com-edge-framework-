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

		public string Name { get; set; }

		/// <summary>
		/// Extra fields for use by channels which have additional metadata per creative.
		/// </summary>
		public Dictionary<SegmentType, Segment> Segments = new Dictionary<SegmentType, Segment>();

		/// <summary>
		/// Extra fields for use by channels which have additional metadata per creative.
		/// </summary>
		public Dictionary<ExtraField, object> ExtraFields = new Dictionary<ExtraField, object>();
	}

	[TypeID(1)]
	public class TextCreative : Creative
	{
		/// <summary>
		/// Title,Body or displayUrl
		/// </summary>
		[FieldIndex(1)]
		public TextCreativeType TextType;


		[FieldIndex(2)]
		public string Text;

		[FieldIndex(3)]
		public string Text2;


	}

	[TypeID(2)]
	public class ImageCreative : Creative
	{
		/// <summary>
		/// Title,Body or displayUrl
		/// </summary>
		[FieldIndex(1)]
		public string AdUnitType;

		[FieldIndex(2)]
		public string ImageUrl;

		[FieldIndex(3)]
		public string ImageSize;
	}

	public enum TextCreativeType : int
	{
		Title = 1,
		Body = 2,
		DisplayUrl = 3
	}
}
