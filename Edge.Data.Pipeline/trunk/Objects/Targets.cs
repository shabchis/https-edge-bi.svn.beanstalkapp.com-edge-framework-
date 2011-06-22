using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Edge.Data.Objects.Reflection;

namespace Edge.Data.Objects
{
	public abstract class Target : MappedType
	{
		public string OriginalID;
		public string DestinationUrl;

		// This is for use by channels which have extra data per target
		public Dictionary<TargetCustomField, object> CustomFields=new Dictionary<TargetCustomField,object>();

		/// <summary>
		/// Segments per target, e.g. tracker on a keyword.
		/// </summary>
		public Dictionary<Segment, SegmentValue> SegmentValues;
	}



	[TypeID(2)]
	public class KeywordTarget : Target
	{
		[FieldIndex(1)]
		public string Keyword;

		[FieldIndex(2)]
		public KeywordMatchType MatchType;
	}

	public enum KeywordMatchType
	{
		Unidentified = 0,
		Broad = 1,
		Phrase = 2,
		Exact = 3,
		Content = 4,
		WebSite = 5
	};

	[TypeID(3)]
	public class GenderTarget : Target
	{
		[FieldIndex(1)]
		public Gender Gender;
	}

	public enum Gender
	{
		Male = 1,
		Female = 2,
		UnSpecified=3
	}

	[TypeID(4)]
	public class AgeTarget : Target
	{
		[FieldIndex(1)]
		public int FromAge;
		[FieldIndex(2)]
		public int ToAge;
	}
}
