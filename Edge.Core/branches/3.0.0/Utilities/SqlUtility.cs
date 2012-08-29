using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Utilities
{
	public static class SqlUtility
	{
		public static object NullIf(object obj, object value)
		{
			if (obj == null)
				return DBNull.Value;
			else
				return value;
		}
	}
}
