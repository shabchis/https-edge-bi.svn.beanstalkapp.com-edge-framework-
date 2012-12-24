using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class EdgeType
	{
		public int TypeID;
		public string Name;
		public Type ClrType;
		public string TableName;

		public Account Account;
		public Channel Channel;
	}
}
