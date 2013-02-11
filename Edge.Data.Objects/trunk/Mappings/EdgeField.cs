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
					.Map<int>(EdgeType.Properties.TypeID, "FieldTypeID")
					.Map<Type>(EdgeType.Properties.ClrType, clrType => clrType
						.Set(context => context.HasField("FieldClrType") ? Type.GetType(context.GetField<string>("FieldClrType")) : null)
						//.Set(context => context.IfFieldPresent<string>("FieldClrType", value => Type.GetType(value), null))
					)
				)
				.Map<EdgeType>(EdgeField.Properties.ParentEdgeType, edgeType => edgeType
					.Map<int>(EdgeType.Properties.TypeID, "ParentTypeID")
					.Map<Type>(EdgeType.Properties.ClrType, clrType => clrType
						.Set(context => context.HasField("ParentClrType") ? Type.GetType(context.GetField<string>("ParentClrType")) : null)
					)
				)
				.Map<string>(EdgeField.Properties.ColumnPrefix, "ColumnPrefix")
				.Map<int>(EdgeField.Properties.ColumnIndex, "ColumnIndex")
			;

		}
	}
}
