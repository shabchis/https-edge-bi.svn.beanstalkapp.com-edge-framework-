using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class MetaProperty
	{
		public int ID;
		public string PropertyName;
		public Account Account;
		public Channel Channel;
		public Type BaseValueType;
		public MetaPropertyOptions Options;
	}

	[Flags]
	public enum MetaPropertyOptions
	{
		None = 0x0
	}

}
