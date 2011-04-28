using System;
using System.Text.RegularExpressions;

namespace Edge.Core.Data
{
	/// <summary>
	/// Summary description for Email.
	/// </summary>
	public struct Email
	{
		public static readonly Email Empty;
		private static Regex _fullFormat;
		private string _value;

		static Email()
		{
			_fullFormat = new Regex(@"^([\w-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([\w-]+\.)+))([a-zA-Z]{2,4}|[0-9]{1,3})(\]?)$");

			Empty = new Email(true);
		}

		private Email(bool empty)
		{
			_value = null;
		}

		public Email(string strVal)
		{
			if (strVal == null)
				throw new ArgumentNullException("strVal", "String value cannot be null");

			if (!IsValid(strVal))
				throw new FormatException("The supplied value does not match the format for a " + typeof(Email).FullName);

			this._value = strVal;
		}

		public static bool IsValid(string strVal)
		{
			return _fullFormat.IsMatch(strVal);
		}

		public override bool Equals(object obj)
		{
			if (obj is Email)
			{
				return this._value == ((Email) obj)._value;
			}
			else if (obj is string)
			{
				return this.ToString() == (string) obj;
			}
			else
			{
				return false;
			}
		}

		public override string ToString()
		{
			return _value == null ? String.Empty : _value;
		}


		public override int GetHashCode()
		{
			return ToString().GetHashCode();
		}

		//		public static implicit operator string(Email val)
		//		{
		//			return val.ToString();
		//		}
		//
		//		public static implicit operator Email(string strVal)
		//		{
		//			return new Email(strVal);
		//		}

		public static bool operator==(Email val, string strVal)
		{
			return val.Equals(strVal);
		}

		public static bool operator!=(Email val, string strVal)
		{
			return !val.Equals(strVal);
		}

		public static bool operator==(Email val1, Email val2)
		{
			return val1.Equals(val2);
		}

		public static bool operator!=(Email val1, Email val2)
		{
			return !val1.Equals(val2);
		}
	}
}
