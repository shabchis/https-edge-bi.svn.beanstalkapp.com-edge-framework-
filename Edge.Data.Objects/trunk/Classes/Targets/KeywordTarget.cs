using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class KeywordTarget : Target
	{
		public KeywordMatchType MatchType;
	}

	public enum KeywordMatchType
	{
		Unidentified = 0,
		Broad = 1,
		Phrase = 2,
		Exact = 3
	};
}
