using System;
using System.Linq;
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

		public EdgeField this[string fieldName]
		{
			get
			{
				// TODO: verify that only one field is defined with this name!!!
				return this.Fields
					.Where(typeField => typeField.Field.Name == fieldName)
					.Select(typeField => typeField.Field)
					.FirstOrDefault();
			}
		}
	}
}
