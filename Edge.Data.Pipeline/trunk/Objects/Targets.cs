﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Edge.Data.Objects.Reflection;

namespace Edge.Data.Objects
{
	//==================================================================================
	// BASE CLASSES

	public abstract class Target : MappedType
	{
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
		public Dictionary<Segment, SegmentValue> Segments=new Dictionary<Segment,SegmentValue>();
	}

	//==================================================================================
	// TARGET TYPES

	[TypeID(2)]
	public class KeywordTarget : Target
	{
		[FieldIndex(1)]
		public string Keyword;

		[FieldIndex(2)]
		public KeywordMatchType MatchType;

		[FieldIndex(3)]
		public string QualityScore;
	}

	public enum KeywordMatchType
	{
		Unidentified = 0,
		Broad = 1,
		Phrase = 2,
		Exact = 3
	};


	[TypeID(5)]
	public class PlacementTarget : Target
	{
		[FieldIndex(1)]
		public string Placement;

		[FieldIndex(2)]
		public PlacementType PlacementType;
	}

	public enum PlacementType
	{
		Unidentified = 0,
		Automatic = 4,
		Managed = 5
	}


	[TypeID(3)]
	public class GenderTarget : Target
	{
		[FieldIndex(1)]
		public Gender Gender;
	}

	public enum Gender
	{
		Unspecified=0,
		Male = 1,
		Female = 2
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
