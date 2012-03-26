using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects.Reflection;

namespace Edge.Data.Objects
{
	public abstract class Creative: MappedObject
	{
		public string OriginalID;

		public string Name { get; set; }

		/// <summary>
		/// Extra fields for use by channels which have additional metadata per creative.
		/// </summary>
		public Dictionary<Segment, SegmentObject> Segments = new Dictionary<Segment, SegmentObject>();

		/// <summary>
		/// Extra fields for use by channels which have additional metadata per creative.
		/// </summary>
		public Dictionary<ExtraField, object> ExtraFields = new Dictionary<ExtraField, object>();
	}

	[MappedObjectTypeID(1)]
	public class TextCreative : Creative
	{
		/// <summary>
		/// Title,Body or displayUrl
		/// </summary>
		[MappedObjectFieldIndex(1)]
		public TextCreativeType TextType;


		[MappedObjectFieldIndex(2)]
		public string Text;

		[MappedObjectFieldIndex(3)]
		public string Text2;


	}

	[MappedObjectTypeID(2)]
	public class ImageCreative : Creative
	{
		/// <summary>
		/// Title,Body or displayUrl
		/// </summary>
		[MappedObjectFieldIndex(1)]
		public string AdUnitType;

		[MappedObjectFieldIndex(2)]
		public string ImageUrl;

		[MappedObjectFieldIndex(3)]
		public string ImageSize;
	}

	public enum TextCreativeType : int
	{
		Title = 1,
		Body = 2,
		DisplayUrl = 3
	}
}
