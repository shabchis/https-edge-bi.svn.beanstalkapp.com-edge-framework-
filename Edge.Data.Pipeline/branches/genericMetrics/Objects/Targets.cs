using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Edge.Data.Objects.Reflection;

namespace Edge.Data.Objects
{
	//==================================================================================
	// BASE CLASSES

	public abstract class Target : MappedObject
	{
		public Account Account;
		public Channel Channel;

		public string OriginalID;
		public string DestinationUrl;

		public ObjectStatus Status;

		/// <summary>
		/// Extra fields for use by channels which have extra metadata per target.
		/// </summary>
		public Dictionary<ExtraField, object> ExtraFields=new Dictionary<ExtraField,object>();

		/// <summary>
		/// Segments per target, e.g. tracker on a keyword.
		/// </summary>
		public List<SegmentObject> Segments=new List<SegmentObject>();
	}

	//==================================================================================
	// TARGET TYPES

	[MappedObjectTypeID(2)]
	public class KeywordTarget : Target
	{
		[MappedObjectFieldIndex(1)]
		public string Keyword;

		[MappedObjectFieldIndex(2)]
		public KeywordMatchType MatchType;

		[MappedObjectFieldIndex(3)]
		public string QualityScore;
	}

	public enum KeywordMatchType
	{
		Unidentified = 0,
		Broad = 1,
		Phrase = 2,
		Exact = 3
	};


	[MappedObjectTypeID(5)]
	public class PlacementTarget : Target
	{
		[MappedObjectFieldIndex(1)]
		public string Placement;

		[MappedObjectFieldIndex(2)]
		public PlacementType PlacementType;
	}

	public enum PlacementType
	{
		Unidentified = 0,
		Automatic = 4,
		Managed = 5
	}


	[MappedObjectTypeID(3)]
	public class GenderTarget : Target
	{
		[MappedObjectFieldIndex(1)]
		public Gender Gender;
	}

	public enum Gender
	{
		Unspecified=0,
		Male = 1,
		Female = 2
	}

	[MappedObjectTypeID(4)]
	public class AgeTarget : Target
	{
		[MappedObjectFieldIndex(1)]
		public int FromAge;
		[MappedObjectFieldIndex(2)]
		public int ToAge;
	}
}
