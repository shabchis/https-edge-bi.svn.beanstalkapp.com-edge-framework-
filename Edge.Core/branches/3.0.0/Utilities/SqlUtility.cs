using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Data.SqlClient;
using System.Data;

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

		public static T Get<T>(this IDataRecord reader, string field, T nullVal = default(T))
		{
			var val = reader[field];
			if (val is DBNull)
				return nullVal;
			else
				return (T)val;
		}

		public static ConvertT Convert<SourceT, ConvertT>(this IDataRecord reader, string field, Func<SourceT, ConvertT> convertFunc, SourceT nullVal = default(SourceT))
		{
			SourceT val = reader.Get<SourceT>(field, nullVal);
			return convertFunc(val);
		}

		public static bool IsDBNull(this IDataRecord reader, params string[] fields)
		{
			bool isnull = true;
			for (int i = 0; i < fields.Length; i++)
				isnull &= reader[fields[i]] is DBNull;
			return isnull;
		}

		private static Regex _paramFinder = new Regex(@"[@$\?][A-Za-z0-9_]+:[A-Za-z0-9_]+");
		internal static readonly string PrefixInOut = "$";
		internal static readonly string PrefixOut = "?";
		internal static readonly string PrefixIn = "@";

		public static SqlCommand CreateCommand(string text, CommandType type)
		{
			if (text == null)
				throw new ArgumentNullException("text");

			SqlCommand command = new SqlCommand();
			command.CommandText = text;
			command.CommandType = type;

			MatchCollection placeHolders = _paramFinder.Matches(command.CommandText);
			string commandText = command.CommandText;
			int offsetChange = 0;

			for (int i = 0; i < placeHolders.Count; i++)
			{
				string name = placeHolders[i].Value.Substring(1).Split(':')[0];
				string dbtype = placeHolders[i].Value.Substring(1).Split(':')[1];
				string indicator = placeHolders[i].Value[0].ToString();

				// Replace placeholder with actual parameter
				commandText = commandText.Remove(placeHolders[i].Index + offsetChange, placeHolders[i].Length);
				commandText = commandText.Insert(placeHolders[i].Index + offsetChange, PrefixIn + name);
				offsetChange += (PrefixIn + name).Length - placeHolders[i].Length;

				// Ignore the parameter if it already has been added
				if (command.Parameters.Contains(PrefixIn + name))
					continue;

				SqlDbType dbType = (SqlDbType)Enum.Parse(typeof(SqlDbType), dbtype, true);

				SqlParameter param = new SqlParameter();
				param.SqlDbType = dbType;
				param.ParameterName = PrefixIn + name;

				// Set parameter directions
				if (indicator == PrefixInOut)
				{
					param.Direction = ParameterDirection.InputOutput;
				}
				else if (indicator == PrefixOut)
				{
					param.Direction = ParameterDirection.Output;
				}

				// Add the parameter
				command.Parameters.Add(param);
			}

			// For stored procs, leave only the name
			if (command.CommandType == CommandType.StoredProcedure)
				commandText = commandText.Split('(')[0];

			// Replace command text to proper version
			command.CommandText = commandText;

			return command;
		}

		// TODO shirat - implement?
		public static object Normalize(object p)
		{
			throw new NotImplementedException();
		}
	}
}
