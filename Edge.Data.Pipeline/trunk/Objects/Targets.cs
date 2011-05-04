using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract class Target
	{
		public string OriginalID;
		public string DestinationUrl;
	}

	[TargetTypeID(2)]
	public class KeywordTarget : Target
	{
		[TargetFieldIndex(1)]
		public KeywordMatchType MatchType;
		[TargetFieldIndex(2)]
		public string Keyword;
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
	[TargetTypeID(3)]
	public class GenderTarget : Target
	{
		[TargetFieldIndex(1)]
		public Gender Gender;
	}

	public enum Gender
	{
		Male = 1,
		Female = 2,
		UnSpecified=3
	}
	[TargetTypeID(4)]
	public class AgeTarget : Target
	{
		[TargetFieldIndex(1)]
		public int FromAge;
		[TargetFieldIndex(2)]
		public int ToAge;
	}


	#region Attributes

	class TargetTypeIDAttribute : Attribute
	{
		internal int TargetTypeID;
		public TargetTypeIDAttribute(int targetTypeID)
		{
			TargetTypeID = targetTypeID;
		}
	}

	class TargetFieldIndexAttribute : Attribute
	{
		internal int TargetColumnIndex;
		public TargetFieldIndexAttribute(int targetColumnIndex)
		{
			TargetColumnIndex = targetColumnIndex;
		}
	}

	#endregion
}
