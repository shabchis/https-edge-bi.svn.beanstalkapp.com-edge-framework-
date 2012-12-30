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
				.Map<int>(EdgeField.Properties.FieldID, "FieldID")
				.Map<bool>(EdgeField.Properties.IsSystem, "IsSystem")
				.Map<Account>(EdgeField.Properties.Account, account => account
					.Do(context => context.BreakIfNegative("AccountID"))
					.Map<int>(Account.Properties.ID, "AccountID")
				)
				.Map<Channel>(EdgeField.Properties.Channel, channel => channel
					.Do(context => context.BreakIfNegative("ChannelID"))
					.Map<int>(Channel.Properties.ID, "ChannelID")
				)
				.Map<string>(EdgeField.Properties.Name, "Name")
				.Map<string>(EdgeField.Properties.DisplayName, "DisplayName")
				.Map<EdgeType>(EdgeField.Properties.ObjectEdgeType, edgeType => edgeType
					.Map<int>(EdgeType.Properties.TypeID, "ObjectTypeID")
					.Map<Type>(EdgeType.Properties.ClrType, clrType => clrType
						.Set(context => Type.GetType(context.GetField<string>("ObjectClrType")))
					)
				)
				.Map<EdgeType>(EdgeField.Properties.FieldEdgeType, edgeType => edgeType
					.Map<int>(EdgeType.Properties.TypeID, "FieldTypeID")
					.Map<Type>(EdgeType.Properties.ClrType, clrType => clrType
						.Set(context => Type.GetType(context.GetField<string>("FieldClrType")))
					)
				)
				.Map<Type>(EdgeField.Properties.FieldClrType, clrType => clrType
					.Set(context => Type.GetType(context.GetField<string>("FieldClrType")))
				)
				.Map<string>(EdgeField.Properties.ColumnPrefix, "ColumnPrefix")
				.Map<int>(EdgeField.Properties.ColumnIndex, "ColumnIndex")
			;

		}
	}
}
