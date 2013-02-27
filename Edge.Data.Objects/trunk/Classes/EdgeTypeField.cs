using System;

namespace Edge.Data.Objects
{
	public partial class EdgeTypeField
	{
		public EdgeField Field;
		public string ColumnName;
		public bool IsIdentity;

		public string IdentityColumnName
		{
			get { return Field.FieldEdgeType == null ? ColumnName : String.Format("{0}_gk", ColumnName); }
		}
	}
}
