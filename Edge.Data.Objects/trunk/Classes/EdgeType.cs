using System;
using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class EdgeType
	{
		public int TypeID;
		public EdgeType BaseEdgeType;
		public Type ClrType;
		public string Name;
		public string TableName;
		public bool IsAbstract;

		public Account Account;
		public Channel Channel;

		public List<EdgeTypeField> Fields = new List<EdgeTypeField>();
	}
}
