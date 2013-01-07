using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract partial class EdgeField
	{
		public int FieldID;
		public EdgeType ParentEdgeType;

		public string Name;
		public string DisplayName;

		public bool IsConnection;
		public EdgeType ConnectionEdgeType;
		public Type ConnectionClrType;

		public string ColumnType;
		public int ColumnIndex;
	}

}
