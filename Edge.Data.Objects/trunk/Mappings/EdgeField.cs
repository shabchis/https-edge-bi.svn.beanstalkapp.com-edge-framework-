using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;

namespace Edge.Data.Objects
{
	public partial class EdgeField
	{
		public static class Mappings
		{
			public static Mapping<EdgeField> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<EdgeField>()
				.Type(field: "FieldType")
				.Identity(EdgeField.Identities.Default)
				.Map<int>(EdgeField.Properties.FieldID, "FieldID")
				.Map<string>(EdgeField.Properties.Name, "Name")
				.Map<string>(EdgeField.Properties.DisplayName, "DisplayName")
				.Map<EdgeType>(EdgeField.Properties.FieldEdgeType, edgeType => edgeType
					.Identity(EdgeType.Identities.Default)
					.Map<int>(EdgeType.Properties.TypeID, "FieldTypeID")
					.Map<Type>(EdgeType.Properties.ClrType, "FieldClrType", typeName => Type.GetType(typeName.ToString()))
				)
			;

		}
	}
}
