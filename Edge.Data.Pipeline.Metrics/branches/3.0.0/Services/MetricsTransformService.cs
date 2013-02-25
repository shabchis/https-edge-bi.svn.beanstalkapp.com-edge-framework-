using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Edge.Core.Configuration;
using Edge.Core.Services;
using Edge.Core.Utilities;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Managers;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Services;

namespace Edge.Data.Pipeline.Metrics.Services
{
	public class MetricsTransformService : PipelineService
	{
		private const int ACCOUNT_ID = -1;
		public Dictionary<string, EdgeType> EdgeTypes { get; private set; }
		public List<ExtraField> ExtraFields { get; private set; }
		public Dictionary<EdgeField, EdgeFieldDependencyInfo> Dependencies { get; private set; }

		protected override ServiceOutcome DoPipelineWork()
		{
			var checksumThreshold = Configuration.Parameters.Get<string>(Consts.ConfigurationOptions.ChecksumTheshold, false);
			var options = new MetricsDeliveryManagerOptions
			{
				SqlTransformCommand = Configuration.Parameters.Get<string>(Consts.AppSettings.SqlTransformCommand),
				ChecksumThreshold = checksumThreshold == null ? 0.01 : double.Parse(checksumThreshold)
			};

			BuildObjectDependencies();

			using (var importManager = new MetricsDeliveryManager(InstanceID, options: options))
			{
				// TODO: need this? Only check tickets, don't check conflicts
				HandleConflicts(importManager, DeliveryConflictBehavior.Ignore, getBehaviorFromConfiguration: false);

				// set object dependencies
				importManager.EdgeObjectDependencies = Dependencies;

				// perform transform
				Log(String.Format("Start transform deliver '{0}'", Delivery.DeliveryID), LogMessageType.Information);
				importManager.Transform(new[] {Delivery});
				Log(String.Format("Finished transform deliver '{0}'", Delivery.DeliveryID), LogMessageType.Information);
			}
			return ServiceOutcome.Success;
		}

		private void BuildObjectDependencies()
		{
			LoadEdgeTypes();
			LoadEdgeFields();
			LoadEdgeTypeFields();

			// build object dependencies: child with parent list
			Dependencies = ExtraFields.Where(f => f.FieldEdgeType != null).ToDictionary(f => f as EdgeField, f => new EdgeFieldDependencyInfo { Field = f });
			foreach (var field in Dependencies.Values)
			{
				FindFieldDependencies(field.Field);
			}

			// set dependency depth of each object 
			foreach (var field in ExtraFields.Where(x => x.FieldEdgeType != null))
			{
				Dependencies[field].Depth = SetFieldDependencyDepth(field);
			}
			
		}

		private int SetFieldDependencyDepth(EdgeField field)
		{
			var maxDepth = 0;
			foreach (var childField in field.FieldEdgeType.Fields)
			{
				if (childField.Field.FieldEdgeType != null)
				{
					var childDepth = SetFieldDependencyDepth(childField.Field);
					maxDepth = maxDepth > childDepth + 1 ? maxDepth : childDepth + 1 ;
				}
			}
			return maxDepth;
		}

		private void FindFieldDependencies(EdgeField field)
		{
			foreach (var childField in field.FieldEdgeType.Fields.Where(x => x.Field.FieldEdgeType != null))
			{
				if (!Dependencies[childField.Field].DependentFields.ContainsKey(field))
				{
					Dependencies[childField.Field].DependentFields.Add(field, new EdgeTypeField {Field = field, ColumnName = childField.ColumnName, IsIdentity = childField.IsIdentity});
					FindFieldDependencies(childField.Field);
				}
			}
		}

		private void LoadEdgeTypes()
		{
			EdgeTypes = new Dictionary<string, EdgeType>();
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("MD_EdgeType_Get", CommandType.StoredProcedure);
					cmd.Parameters.AddWithValue("@accountID", ACCOUNT_ID);
					cmd.Connection = connection;
					connection.Open();

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var type = new EdgeType
							{
								TypeID = int.Parse(reader["TypeID"].ToString()),
								Name = reader["Name"].ToString(),
								TableName = reader["TableName"].ToString(),
								ClrType = Type.GetType(reader["ClrType"].ToString())
							};
							EdgeTypes.Add(type.Name, type);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get edge types from DB", ex);
			}
		}

		private void LoadEdgeFields()
		{
			ExtraFields = new List<ExtraField>();
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("MD_EdgeField_Get", CommandType.StoredProcedure);
					cmd.Parameters.AddWithValue("@accountID", ACCOUNT_ID);
					cmd.Connection = connection;
					connection.Open();

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							var field = new ExtraField
							{
								FieldID = int.Parse(reader["FieldID"].ToString()),
								Name = reader["Name"].ToString(),
								DisplayName = reader["DisplayName"].ToString(),
								FieldEdgeType = EdgeTypes.Values.FirstOrDefault(x => x.TypeID == int.Parse(reader["FieldTypeID"].ToString())),
							};
							ExtraFields.Add(field);
						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get extra fields from DB", ex);
			}
		}

		private void LoadEdgeTypeFields()
		{
			try
			{
				using (var connection = new SqlConnection(AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects)))
				{
					var cmd = SqlUtility.CreateCommand("MD_EdgeTypeField_Get", CommandType.StoredProcedure);
					cmd.Parameters.AddWithValue("@accountID", ACCOUNT_ID);
					cmd.Connection = connection;
					connection.Open();

					using (var reader = cmd.ExecuteReader())
					{
						while (reader.Read())
						{
							// find parent edge type nad edge field
							var parentTypeId = int.Parse(reader["ParentTypeID"].ToString());
							var fieldtId = int.Parse(reader["FieldID"].ToString());

							var parentType = EdgeTypes.Values.FirstOrDefault(x => x.TypeID == parentTypeId);
							if (parentType == null)
								throw new ConfigurationErrorsException(String.Format("Configuration error: Unknown parent edge type {0} while loading edge type fields", parentTypeId));

							var field = ExtraFields.FirstOrDefault(x => x.FieldID == fieldtId);
							if (field == null)
								throw new ConfigurationErrorsException(String.Format("Configuration error: Unknown edge field {0} while loading edge type fields", fieldtId));

							var typeField = new EdgeTypeField
							{
								ColumnName = reader["ColumnName"].ToString(),
								IsIdentity = bool.Parse(reader["IsIdentity"].ToString()),
								Field = field
							};

							// add edge field to parent edge type
							if (!parentType.Fields.Contains(typeField))
								parentType.Fields.Add(typeField);
							else
								throw new ConfigurationErrorsException(String.Format("Configuration error: Field {0} already exists in parent edge type {1}", field.Name, parentType.Name));

						}
					}
				}
			}
			catch (Exception ex)
			{
				throw new Exception("Error while trying to get extra fields from DB", ex);
			}
		}
		
	}
}
