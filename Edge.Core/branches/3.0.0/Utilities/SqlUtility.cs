using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Utilities
{
	public static class SqlUtility
	{
		public static object SqlValue(object obj, object nullValue, Func<object> valueFunc = null)
		{
			if (Object.Equals(obj, null) || Object.Equals(obj, nullValue))
				return DBNull.Value;
			else
				return valueFunc == null ? obj : valueFunc();
		}

		public static object SqlValue(object obj, Func<object> valueFunc = null)
		{
			return SqlValue(obj, null, valueFunc);
		}

		public static T ClrValue<T>(object dbValue) where T : class
		{
			return dbValue is DBNull ? null : (T)dbValue;
		}

		public static T ClrValue<T>(object dbValue, T emptyVal)
		{
			return dbValue is DBNull ? emptyVal : (T) dbValue;
		}

		public static T ClrValue<R, T>(object dbValue, Func<R, T> convertFunc, T emptyVal)
		{
			return dbValue is DBNull ? emptyVal : convertFunc((R)dbValue);
		}
	}
}
