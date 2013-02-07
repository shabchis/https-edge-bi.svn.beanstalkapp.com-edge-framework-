using System;

namespace Edge.Data.Objects
{
	public abstract partial class EdgeField
	{
		public int FieldID;
		
		public string Name;
		public string DisplayName;

		public EdgeType FieldEdgeType;
		public EdgeType ParentEdgeType;

		public string ColumnPrefix;
		public int ColumnIndex;
	}

}
