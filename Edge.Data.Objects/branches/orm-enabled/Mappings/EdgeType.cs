using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Queries;
using Eggplant.Entities.Persistence.SqlServer;
using System.Data;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class EdgeType
	{
		public static class Mappings
		{
			public static Mapping<EdgeType> Default = EdgeUtility.EntitySpace.CreateMapping<EdgeType>(edgeType => edgeType
				.Identity(EdgeType.Identities.Default)
				.Map<int>(EdgeType.Properties.TypeID, "TypeID")
				.Map<EdgeType>(EdgeType.Properties.BaseEdgeType, baseEdgeType => baseEdgeType
					.Do(context=>context.NullIf<object>("BaseTypeID", id => id == null))
					.Map<int>(EdgeType.Properties.TypeID, "BaseTypeID")
				)
				.Map<Type>(EdgeType.Properties.ClrType, "ClrType",
					convertIn: EdgeUtility.Conversions.TypeConvertIn,
					convertOut: EdgeUtility.Conversions.TypeConvertOut
				)
				.Map<string>(EdgeType.Properties.Name, "Name")
				.Map<string>(EdgeType.Properties.TableName, "TableName")
				.Map<bool>(EdgeType.Properties.IsAbstract, "IsAbstract")
				.Map<Account>(EdgeType.Properties.Account, account => account
					.Do(context => context.NullIf<int>("AccountID", id => id == -1))
					.Map<int>(Account.Properties.ID, "AccountID")
				)
				.Map<Channel>(EdgeType.Properties.Channel, channel => channel
					.Do(context => context.NullIf<int>("ChannelID", id => id == -1))
					.Map<int>(Channel.Properties.ID, "ChannelID")
				)

				.MapListFromSubquery<EdgeType, EdgeTypeField>(EdgeType.Properties.Fields, "EdgeTypeFields",
					parent => parent
						.Identity(EdgeType.Identities.Default)
						.Map<int>(EdgeType.Properties.TypeID, "ParentTypeID")
					,
					item => item
						.Map<EdgeField>(EdgeTypeField.Properties.Field, field => field
							.UseMapping(EdgeField.Mappings.Default)
						)
						.Map<string>(EdgeTypeField.Properties.ColumnName, "ColumnName")
						.Map<bool>(EdgeTypeField.Properties.IsIdentity, "IsIdentity")
				)
			);


			public static Mapping<EdgeType> Save = EdgeUtility.EntitySpace.CreateMapping<EdgeType>(edgeType => edgeType
				.Map<int>(EdgeType.Properties.TypeID, "TypeID")
				.Map<Type>(EdgeType.Properties.ClrType, "ClrType",
					convertIn: EdgeUtility.Conversions.TypeConvertIn,
					convertOut: EdgeUtility.Conversions.TypeConvertOut
				)
				.Map<string>(EdgeType.Properties.Name, "Name")
				.Map<List<EdgeTypeField>>(EdgeType.Properties.Fields, fields => fields
					.Subquery<EdgeTypeField>("EdgeTypeFields", subquery => subquery
						
						.WhenOutbound(outbound => outbound
							.OutboundSource(context => fields.FromContext(context))
						)

						.Map<EdgeField>(EdgeTypeField.Properties.Field, field => field
							.UseMapping(EdgeField.Mappings.Default)
						)
						.Map<string>(EdgeTypeField.Properties.ColumnName, "ColumnName")
						.Map<bool>(EdgeTypeField.Properties.IsIdentity, "IsIdentity")

						.WhenInbound(inbound => inbound
							.Map<EdgeType>("parent", parent => parent
								.Identity(EdgeType.Identities.Default)
								.Map<int>(EdgeType.Properties.TypeID, "ParentTypeID")
							)
							.Do(context => context.GetVariable<EdgeType>("parent").Fields.Add(context.MappedValue))
						)

						
					)
				)
				.Map<List<EdgeTypeField>>(EdgeType.Properties.Fields, fields => fields

					.Subquery<EdgeTypeField, EdgeType>("EdgeTypeFields",
						list => list.AsEnumerable(),

						subquery => subquery
							.Parent<EdgeType>(edgeType, parent => parent
								.Map<EdgeType>("parent", parent => parent
									.Identity(EdgeType.Identities.Default)
									.Map<int>(EdgeType.Properties.TypeID, "ParentTypeID")
								)
							)

							.Map<EdgeField>(EdgeTypeField.Properties.Field, field => field
								.UseMapping(EdgeField.Mappings.Default)
							)
							.Map<string>(EdgeTypeField.Properties.ColumnName, "ColumnName")
							.Map<bool>(EdgeTypeField.Properties.IsIdentity, "IsIdentity")

							.WhenInbound(inbound => inbound
								.Do(context => fields.FromContext(context).Add(context.MappedValue))
							)


					)
				)
				.MapSubquery<List<EdgeTypeField>, EdgeTypeField>(EdgeType.Properties.Fields, "EdgeTypeFields", fields => fields

					.MapItem(item => item
						.Map<EdgeField>(EdgeTypeField.Properties.Field, field => field
							.UseMapping(EdgeField.Mappings.Default)
						)
						.Map<string>(EdgeTypeField.Properties.ColumnName, "ColumnName")
						.Map<bool>(EdgeTypeField.Properties.IsIdentity, "IsIdentity")
						
						.WhenOutbound(outbound => outbound
							.Enumerate(context => fields.FromContext(context))
						)
					)

					.WhenInbound(inbound => inbound
						.Map<EdgeType>("parent", parent => parent
							.Identity(EdgeType.Identities.Default)
							.Map<int>(EdgeType.Properties.TypeID, "ParentTypeID")
						)
						.Do(context => fields.FromContext(context).Add(context.GetVariable<EdgeTypeField>("item")))
					)

					
				)
			);
		}

		public static class Identities
		{
			public static IdentityDefinition Default = new IdentityDefinition(EdgeType.Properties.TypeID);
		}

		public static class Queries
		{
			public static QueryTemplate<EdgeType> Get = EdgeUtility.EntitySpace.CreateQueryTemplate<EdgeType>(Mappings.Default)
				
				.Input<Account>("account", required: false)
				.Input<Channel>("channel", required: false)

				.RootSubquery(EdgeUtility.GetSql<EdgeType>("Get"), init => init
					.ParamFromInput("@accountID", "account", convertOut: EdgeUtility.Conversions.ConvertAccountToID)
					.ParamFromInput("@channelID", "channel", convertOut: EdgeUtility.Conversions.ConvertChannelToID)
				)
				.Subquery("EdgeTypeFields", EdgeUtility.GetSql<EdgeType>("Get/EdgeTypeFields"), init => init
					.ParamFromInput("@accountID", "account", convertOut: EdgeUtility.Conversions.ConvertAccountToID)
					.ParamFromInput("@channelID", "channel", convertOut: EdgeUtility.Conversions.ConvertChannelToID)
				)
			;

			public static QueryTemplate<Nothing> Save = EdgeUtility.EntitySpace.CreateQueryTemplate<Nothing>()

				.Input<EdgeType>("toSave", required: true)

				.RootSubquery(EdgeUtility.GetSql<EdgeType>("Save"), init => init
					.ParamsFromMappedInput("toSave", Mappings.Default)
				)
				.Subquery("EdgeTypeFields", EdgeUtility.GetSql<EdgeType>("Save/EdgeTypeFields"), init => init
					.ParamsFromMappedInput("toSave", Mappings.Default)
				)
			;
		}

		public static IEnumerable<EdgeType> Get(Account account = null, Channel channel = null, PersistenceConnection connection = null)
		{
			return Queries.Get.Start()
				.Input<Account>("account", account)
				.Input<Channel>("channel", channel)
				.Connect(connection)
				.Execute();
		}
	}
}
