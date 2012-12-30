using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class EdgeField
	{
		public int FieldID;
		public bool IsSystem;
		public Account Account;
		public Channel Channel;

		public string Name;
		public string DisplayName;
	
		public EdgeType ObjectEdgeType;

		public EdgeType FieldEdgeType;
		public Type FieldClrType;
	
		public string ColumnPrefix;
		public int ColumnIndex;
	}
}
