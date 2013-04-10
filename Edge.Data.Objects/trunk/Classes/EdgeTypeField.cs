using System;

namespace Edge.Data.Objects
{
	public partial class EdgeTypeField
	{
		public EdgeField Field;
		public string ColumnName;
		public bool IsIdentity;

		public string ColumnNameGK
		{
			get { return Field.FieldEdgeType == null ? ColumnName : String.Format("{0}_gk", ColumnName); }
		}

		public string ColumnNameTK
		{
			get { return Field.FieldEdgeType == null ? ColumnName : String.Format("{0}_tk", ColumnName); }
		}

		public string FieldNameGK
		{
			get { return Field.FieldEdgeType == null ? Field.Name : String.Format("{0}_gk", Field.Name); }
		} 
	}
}
