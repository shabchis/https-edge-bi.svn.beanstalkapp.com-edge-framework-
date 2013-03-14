using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public enum FlagsOperator
	{
		ContainsAll = 2,
		ContainsAny = 1,
		Excludes = 0
	}

	public struct FlagsQuery
	{
		public FlagsOperator Operator;
		public int Value;

		public FlagsQuery(FlagsOperator @operator, int value)
		{
			this.Operator = @operator;
			this.Value = value;
		}

		public static FlagsQuery By(FlagsOperator @operator, int value)
		{
			return new FlagsQuery(@operator, value);
		}

	}
}
