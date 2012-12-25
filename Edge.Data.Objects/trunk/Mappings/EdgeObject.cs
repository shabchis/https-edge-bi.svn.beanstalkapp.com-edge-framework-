using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Queries;

namespace Edge.Data.Objects
{
	public partial class EdgeObject
	{
		public static class Mappings
		{
			public static Mapping<EdgeObject> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<EdgeObject>()

				.Map<long>(EdgeObject.Properties.GK, "GK")
				.Map<EdgeType>(EdgeObject.Properties.EdgeType, edgeType => edgeType
					.Map<int>(EdgeType.Properties.TypeID, "TypeID")
				)
				.Map<Account>(EdgeObject.Properties.Account, account => account
					.Do(context => context.BreakIfNegative("AccountID"))
					.Map<int>(Account.Properties.ID, "AccountID")
				)

				/*
				// Connections
				.Map<Dictionary<ConnectionDefinition, EdgeObject>>(EdgeObject.Properties.Connections, connections => connections
					.Subquery("Connections", subquery => subquery
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
				*/

				.MapDictionaryFromSubquery<EdgeObject, ConnectionDefinition, EdgeObject>(EdgeObject.Properties.Connections, "Connections",
					parent => parent
						.DynamicEdgeObject("FromGK", "FromTypeID", "FromClrType"),
					key => key
						.Map<int>(ConnectionDefinition.Properties.ID, "ConnectionDefID"),
					value => value
						.DynamicEdgeObject("ToGK", "ToTypeID", "ToClrType")
				)

			;

			
		}

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
						.DbParam("@objectType", query => query.Param<Type>("objectType").FullName)
						.DbParam("@gk", query => query.Param<long>("gk"))
						.ParseEdgeTemplate()
					)

				.Subquery("Connections",
					EdgeObjectsUtility.GetEdgeTemplate("EdgeObject.sql", "EdgeObject.GetByGK.Connections"),
					subquery => subquery
						.DbParam("@objectType", query => query.Param<Type>("objectType").FullName)
						.DbParam("@objectGK", query => query.Param<long>("gk"))
						.ParseEdgeTemplate()
					)

			;
		}
	}
}
