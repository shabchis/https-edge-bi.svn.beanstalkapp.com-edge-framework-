using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
{
	public struct GKey
	{
		GKeyType _type;
		long _value;

		public GKey(GKeyType type, long value)
		{
			_type = type;
			_value = value;
		}

		public GKeyType Type
		{
			get { return _type; }
		}

		public long Value
		{
			get { return _value; }
		}

		public override bool Equals(object obj)
		{
			return obj is GKey && ((GKey)obj)._type == this._type && ((GKey)obj)._value == this._value;
		}
	}

	public enum GKeyType
	{
		Tracker,
		Keyword,
		Creative,
		Site,
		Campaign,
		Adgroup,
		AdgroupKeyword,
		AdgroupCreative,
		AdgroupSite
	}
}
