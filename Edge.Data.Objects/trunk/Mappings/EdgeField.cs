using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Model;
using Eggplant.Entities.Queries;
using Eggplant.Entities.Persistence.SqlServer;

namespace Edge.Data.Objects
{
	public partial class EdgeField
	{
		public static class Mappings
		{
			public static Mapping<EdgeField> Default = EdgeUtility.EntitySpace.CreateMapping<EdgeField>()
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

		public static class Identities
		{
			public static IdentityDefinition Default = new IdentityDefinition(EdgeField.Properties.FieldID);
		}

		public static class Queries
		{
			public static QueryTemplate<EdgeField> GetByID = EdgeUtility.EntitySpace.CreateQueryTemplate<EdgeField>(Mappings.Default)
				.Param<int>("fieldID")
				.RootSubquery(EdgeUtility.GetSql<EdgeField>("GetByID"), init => init
					.PersistenceParam("@fieldID", fromQueryParam: "fieldID")
				)
			;

			public static QueryTemplate<EdgeField> Get = EdgeUtility.EntitySpace.CreateQueryTemplate<EdgeField>(Mappings.Default)
				.Param<int>("accountID", required: false, defaultValue: -1)
				.Param<int>("channelID", required: false, defaultValue: -1)
				.RootSubquery(EdgeUtility.GetSql<EdgeField>("Get"), init => init
					.PersistenceParam("@accountID", fromQueryParam: "accountID")
					.PersistenceParam("@channelID", fromQueryParam: "channel")
				)
			;

		}

		public static EdgeField Get(int fieldID, PersistenceConnection connection = null)
		{
			return Queries.GetByID.Start()
				.Param<int>("fieldID", fieldID)
				.Connect(connection)
				.Execute()
				.FirstOrDefault()
			;
		}

		public static IEnumerable<EdgeField> Get(int accountID = -1, int channelID = -1, PersistenceConnection connection = null)
		{
			return Queries.Get.Start()
				.Param<int>("accountID", accountID)
				.Param<int>("channelID", channelID)
				.Connect(connection)
				.Execute()
			;
		}
	}
}
