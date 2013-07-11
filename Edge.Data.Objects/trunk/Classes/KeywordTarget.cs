using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class KeywordTarget : Target
	{
		public string Value;
		public KeywordMatchType MatchType;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (MatchType != null) yield return new ObjectDimension
			{
				Field = EdgeType["KeywordMatchType"],
				Value = MatchType
			};
		}
	}

	public class KeywordMatchType : StringValue { }

	//public enum KeywordMatchType
	//{
	//	Unidentified = 0,
	//	Broad = 1,
	//	Phrase = 2,
	//	Exact = 3
	//};
}
