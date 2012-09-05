using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract partial class EdgeObject
	{
		public ulong GK;
		public string Name;

		public Account Account;

		public Dictionary<MetaProperty, object> MetaProperties;
	}
}
