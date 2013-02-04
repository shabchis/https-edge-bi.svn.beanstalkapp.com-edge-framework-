using System;

namespace Edge.Data.Objects
{
	public abstract partial class EdgeField
	{
		public int FieldID;
		
		public string Name;
		public string DisplayName;

		public string ColumnType;
		public int ColumnIndex;
		
		public Type FieldClrType;
		public EdgeType FieldEdgeType;
		public EdgeType ParentEdgeType;
	}

}
