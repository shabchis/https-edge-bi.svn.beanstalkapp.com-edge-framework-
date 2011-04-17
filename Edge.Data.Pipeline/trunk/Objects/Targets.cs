using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	public abstract class Target
	{
		public string OriginalID;
	}

	public class KeywordTarget : Target
	{
		public KeywordMatchType MatchType;
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

	public class GenderTarget : Target
	{
		public Gender Gender;
	}

	public enum Gender
	{
		Male = 1,
		Female = 2,
		UnSpecified=3
	}

	public class AgeTarget : Target
	{
		public int FromAge;
		public int ToAge;
	}

}
