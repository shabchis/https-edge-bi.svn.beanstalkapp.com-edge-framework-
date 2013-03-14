using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Queries;
using Eggplant;

namespace Edge.Data.Objects
{
	public partial class EdgeObject
	{
		public static class Mappings
		{
			public static Mapping<EdgeObject> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<EdgeObject>(edgeObject => edgeObject

				.Map<long>(EdgeObject.Properties.GK, "GK")
				.Map<EdgeType>(EdgeObject.Properties.EdgeType, edgeType => edgeType
					//.Identity(EdgeType.Identites.Default)
					.Map<int>(EdgeType.Properties.TypeID, "TypeID")
				)
				.Map<Account>(EdgeObject.Properties.Account, account => account
					.Do(context => context.NullIf<int>("AccountID", id => id == -1))
					.Map<int>(Account.Properties.ID, "AccountID")
				)
				/*
				.Map<Dictionary<ExtraField, object>>(EdgeObject.Properties.ExtraFields, extraFields => extraFields
					.Join<ExtraField>(
						context => context
							.Cache.Get<ExtraField>().Where(field =>
								field.ParentEdgeType == null || edgeObject.FromContext(context).EdgeType.IsRelatedTo(field.ParentEdgeType)
							),
						extraField => extraField
							.Map<object>("value", value => value
								.Set(context => {
									ExtraField ef = extraField.FromContext(context);
									if (ef.FieldEdgeType != null)
									{
										return context.GetEdgeObject(
											fieldGK: string.Format("{0}_Field{1}_gk", ef.ColumnType, ef.ColumnIndex),
											fieldTypeID: string.Format("{0}_Field{1}_type", ef.ColumnType, ef.ColumnIndex),
											fieldClrType: string.Format("{0}_Field{1}_clr", ef.ColumnType, ef.ColumnIndex)
										);
									}
									else
									{
										return context.GetField(string.Format("{0}_Field{1}", ef.ColumnType, ef.ColumnIndex));
									}
								})
							)
							.Do(context => context.Target.Add(
								key: extraField.FromContext(context),
								value: context.GetVariable<object>("value"))
							)
					)
				)
				*/
						
				#region Obsolete
				/*
				.Map<Dictionary<ExtraField, object>>(EdgeObject.Properties.ExtraFields, extraFields => extraFields
					.Do(context => {
						EdgeObject edgeObj = edgeObject.FromContext(context);

						foreach (ExtraField field in context.Cache.Get<ExtraField>())
						{
							// Ignore fields associated with different types
							if (field.ParentEdgeType != null && !edgeObj.EdgeType.RelatedTo(field.ParentEdgeType))
								continue;

							object value;
							if (field.FieldEdgeType != null)
							{
								// EdgeType value (with GK)
								int valueTypeID = context.GetField<int>(string.Format("{0}_Field{1}_type", field.ColumnType, field.ColumnIndex), val=>val==null?-1:(int)val);
								long valueGK = context.GetField<long>(string.Format("{0}_Field{1}_gk", field.ColumnType, field.ColumnIndex), val=>val==null?-1:(long)val);
								if (valueTypeID != -1 && valueGK != -1)
								{
									EdgeType fieldEdgeType = context.Cache.Get<EdgeType>(EdgeType.Identities.Default, valueTypeID);
									// TODO: this should create an empty entry
									value = context.Cache.Get(fieldEdgeType.ClrType, EdgeObject.Identities.Default, EdgeObject.Identities.Default.NewIdentity(valueGK));
								}
							}
							else
							{
								// Regular value
								string columnName = string.Format("{0}_Field{1}", field.ColumnPrefix, field.ColumnIndex);
								value = context.GetField(columnName);
								if (!field.FieldClrType.IsAssignableFrom(value.GetType()))
								{
									throw new MappingException(String.Format("Value for extra field '{0}' cannot be mapped from {1}.{2} because it is not of type {3}.",
										field.Name,
										field.ObjectEdgeType.TableName,
										columnName,
										field.FieldClrType.Name
									));
								}

							}

							// Add the value
							current.ExtraFields.Add(field, value);
						}
					})
					
				)
				*/

				/*
				// Connections FULL
				.Map<Dictionary<ConnectionDefinition, EdgeObject>>(EdgeObject.Properties.Connections, connections => connections
					.Join("Connections", subquery => subquery
						.Map<EdgeObject>("parent", parent => parent
							.Map<long>(EdgeObject.Properties.GK, "FromGK")
							//resolve: IdentityResolve.ExistingOnly
						)
						.Map<ConnectionDefinition>("key", key => key
							.Map<int>(ConnectionDefinition.Properties.ID, "ConnectionDefID")
						)
						.Map<EdgeObject>("value", value => value
							.Set(context => (EdgeObject)Activator.CreateInstance(Type.GetType(context.GetField<string>("ToClrType"))))
							.Map<EdgeType>(EdgeObject.Properties.EdgeType, edgeType => edgeType
								.Map<int>(EdgeType.Properties.TypeID, "ToTypeID")
							)
							.Map<long>(EdgeObject.Properties.GK, "ToGK")
						)
						.Do(context => EdgeObject.Properties.Connections.GetValue(context.GetVariable<EdgeObject>("parent")).Add(
								context.GetVariable<ConnectionDefinition>("key"),
								context.GetVariable<EdgeObject>("value")
							)
						)
					)
				)
				
				// Connections SHORTCUT
				.MapDictionaryFromSubquery<EdgeObject, ConnectionDefinition, EdgeObject>(EdgeObject.Properties.Connections, "Connections",
					parent => parent
						.MapEdgeObject("FromGK", "FromTypeID", "FromClrType"),
					key => key
						.Map<int>(ConnectionDefinition.Properties.ID, "ConnectionDefID"),
					value => value
						.MapEdgeObject("ToGK", "ToTypeID", "ToClrType")
				) 
				*/
			#endregion




			);

			
		}

		/*
		public static class Queries
		{
			//public Query<Measure> GetByName = new Query<Measure>()
			public static QueryTemplate<EdgeObject> GetByGK = EdgeObjectsUtility.EntitySpace.CreateQueryTemplate<EdgeObject>(Mappings.Default)
				
				.Param<Type>("objectType", required: true)
				.Param<long>("gk", required: true)
				
				.RootSubquery(
					EdgeObjectsUtility.GetEdgeTemplate("EdgeObject.sql", "EdgeObject.GetByGK.Root"),
					subquery => subquery
						.ConditionalColumn("AccountID", EdgeObject.Properties.Account)
						//.DbParam("@objectType", query => query.Param<Type>("objectType").FullName)
						//.DbParam("@gk", query => query.Param<long>("gk"))
					)

				.Subquery("Connections",
					EdgeObjectsUtility.GetEdgeTemplate("EdgeObject.sql", "EdgeObject.GetByGK.Connections"),
					subquery => subquery
						//.DbParam("@objectType", query => query.Param<Type>("objectType").FullName)
						//.DbParam("@objectGK", query => query.Param<long>("gk"))
					)

			;
		}
		*/
	}
}
