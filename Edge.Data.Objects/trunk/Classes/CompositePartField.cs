namespace Edge.Data.Objects
{
	public partial class CompositePartField: EdgeField
	{
		public Channel Channel;

		public CompositePartField() {}
		public CompositePartField (EdgeField field)
		{
			FieldID = field.FieldID;
			Name = field.Name;
			DisplayName = field.DisplayName;
			ColumnType = field.ColumnType;
			ColumnIndex = field.ColumnIndex;
			FieldClrType = field.FieldClrType;
			FieldEdgeType = field.FieldEdgeType;
			ParentEdgeType = field.ParentEdgeType;
		}
	}
}
