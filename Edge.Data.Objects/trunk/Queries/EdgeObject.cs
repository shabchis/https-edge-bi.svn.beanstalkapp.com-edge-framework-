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
			public static Mapping<EdgeObject> Default = EdgeObjectsUtility.EntitySpace.CreateMapping<EdgeObject>(edgeObject => edgeObject
				.Map<long>(EdgeObject.Properties.GK, "GK")
				.Map<Dictionary<ConnectionDefinition, EdgeObject>>(EdgeObject.Properties.Connections, connections => connections
					.Set(context => new Dictionary<ConnectionDefinition, EdgeObject>())
					.Subquery("Connections", subquery => subquery
						.Map<EdgeObject>("parent", parent => parent
							.Map<long>(EdgeObject.Properties.GK, "FromObjectGK")//,
						//resolve: IdentityResolve.ExistingOnly
						)
						.Map<ConnectionDefinition>("key", key => key
							.Map<int>(ConnectionDefinition.Properties.ID, "ConnectionID")
						)
						.Map<EdgeObject>("value", value => value
							.Set(context => (EdgeObject)Activator.CreateInstance(Type.GetType(context.GetField<string>("ToObjectType"))))
							.Map<long>(EdgeObject.Properties.GK, "ToObjectGK")
						)
						.Do(context => EdgeObject.Properties.Connections.GetValue(context.GetVariable<EdgeObject>("parent")).Add(
								context.GetVariable<ConnectionDefinition>("key"),
								context.GetVariable<EdgeObject>("value")
							)
						)
					)
				)
			);
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
