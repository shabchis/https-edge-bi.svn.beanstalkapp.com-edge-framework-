﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Edge.Core.Configuration;
using Edge.Data.Objects;
using Edge.Data.Pipeline.Metrics.Misc;
using Edge.Data.Pipeline.Metrics.Services;

namespace Edge.Data.Pipeline.Metrics.Managers
{
	/// <summary>
	/// Identity manager - set delivery objects GK according to identity fields
	/// </summary>
	internal class IdentityManager
	{
		#region Properties
		private readonly SqlConnection _deliverySqlConnection;
		private SqlConnection _objectsSqlConnection;

		public List<EdgeFieldDependencyInfo> Dependencies { get; set; }
		public string TablePrefix { get; set; }
		public int AccountId { get; set; }
		#endregion

		#region Ctor

		public IdentityManager(SqlConnection deliveryConnection)
		{
			_deliverySqlConnection = deliveryConnection;
		}
		#endregion

		#region Public Methods

		/// <summary>
		/// Set identity of delivery objects: update delivery objects with existing in EdgeObject DB object GKs 
		/// </summary>
		public void SetExistingObjectsIdentity()
		{
			// load object dependencies
			Dependencies = EdgeObjectConfigLoader.GetEdgeObjectDependencies(AccountId).Values.ToList();

			int maxDependecyDepth = Dependencies.Max(x => x.Depth);
			for (int i = 0; i <= maxDependecyDepth; i++)
			{
				var currentDepth = i;
				foreach (var field in Dependencies.Where(x => x.Depth == currentDepth))
				{
					var deliveryObjects = GetDeliveryObjects(field.Field.FieldEdgeType);
					if (deliveryObjects == null) continue;

					using (var selectGkCommand = PrepareSelectGkCommand(field.Field.FieldEdgeType))
					using (var updateGkCommand = PrepareUpdateGkCommand(field))
					{
						foreach (var deliveryObject in deliveryObjects)
						{
							foreach (var identity in deliveryObject.ObjectIdentities)
							{
								selectGkCommand.Parameters[String.Format("@{0}", identity.FieldName)].Value = identity.Value;
							}

							// select GK frm objects table
							var gk = selectGkCommand.ExecuteScalar();
							// TODO: add log GK was found or not found
							if (gk != null)
							{
								// update delivery with GK if GK was found by identity fields
								updateGkCommand.Parameters["@gk"].Value = gk;
								updateGkCommand.Parameters["@tk"].Value = deliveryObject.TK;
								updateGkCommand.ExecuteNonQuery();
							}
						}
					}
				}
			}
		}

		#endregion

		#region Private Methods

		/// <summary>
		/// Prepare SQL command for retrieving object GK from EdgeObjects DB by identity fields
		/// </summary>
		/// <param name="edgeType"></param>
		/// <returns></returns>
		private SqlCommand PrepareSelectGkCommand(EdgeType edgeType)
		{
			var sqlCmd = new SqlCommand { Connection = ObjectsDbConnection() };

			var whereParamsStr = String.Format("TYPEID=@typeId AND ");
			sqlCmd.Parameters.Add(new SqlParameter("@typeId", edgeType.TypeID));

			foreach (var identityColumnName in edgeType.Fields.Where(x => x.IsIdentity).Select(x => x.IdentityColumnName))
			{
				whereParamsStr = String.Format("{0}{1}=@{1} AND ", whereParamsStr, identityColumnName);
				sqlCmd.Parameters.Add(new SqlParameter(String.Format("@{0}", identityColumnName), null));
			}

			if (whereParamsStr.Length > 5) whereParamsStr = whereParamsStr.Remove(whereParamsStr.Length - 5, 5);

			sqlCmd.CommandText = String.Format("SELECT GK FROM {0} WHERE {1}", edgeType.TableName, whereParamsStr);
			return sqlCmd;
		}

		/// <summary>
		/// Prepare update GK command by TK which contains updates GK in all tables which contains this object:
		/// 1. Object table itself
		/// 2. All dependent parent tables
		/// 3. Metrics table
		/// </summary>
		/// <param name="field"></param>
		/// <returns></returns>
		private SqlCommand PrepareUpdateGkCommand(EdgeFieldDependencyInfo field)
		{
			var sqlCmd = new SqlCommand { Connection = _deliverySqlConnection };
			sqlCmd.Parameters.Add(new SqlParameter("@gk", null));
			sqlCmd.Parameters.Add(new SqlParameter("@tk", null));
			sqlCmd.Parameters.Add(new SqlParameter("@typeId", field.Field.FieldEdgeType.TypeID));

			var sqlList = new List<string>();
			// add edge type table update
			sqlList.Add(String.Format("UPDATE {0} SET GK=@gk WHERE TK=@tk AND TYPEID=@typeId;\n", GetDeliveryTableName(field.Field.FieldEdgeType.TableName)));

			// create udate SQL for each dependent object 
			foreach (var dependent in field.DependentFields)
			{
				sqlList.Add(String.Format("UPDATE {0} SET {1}_GK=@gk WHERE {1}_TK=@tk AND {1}_TYPE=@typeId AND TYPEID=@{2}_typeId;\n",
											GetDeliveryTableName(dependent.Key.FieldEdgeType.TableName),
											dependent.Value.ColumnName, dependent.Value.Field.Name));

				sqlCmd.Parameters.Add(new SqlParameter(String.Format("@{0}_typeId", dependent.Value.Field.Name), dependent.Value.Field.FieldEdgeType.TypeID));
			}

			// update Metrics SQL
			sqlList.Add(String.Format("UPDATE {0} SET {1}_GK=@gk WHERE {1}_TK=@tk;\n", GetDeliveryTableName("Metrics"), field.Field.Name));

			// combine all SQLs into one sql command
			sqlCmd.CommandText = String.Join(String.Empty, sqlList);

			return sqlCmd;
		}

		private SqlConnection ObjectsDbConnection()
		{
			if (_objectsSqlConnection == null)
			{
				var connectionString = AppSettings.GetConnectionString(typeof(MetricsProcessorServiceBase), Consts.ConnectionStrings.Objects);
				_objectsSqlConnection = new SqlConnection(connectionString);
				_objectsSqlConnection.Open();
			}
			return _objectsSqlConnection;
		}

		/// <summary>
		/// Load objects by type from Delivery DB (by identity fields)
		/// </summary>
		/// <param name="edgeType"></param>
		/// <returns></returns>
		private IEnumerable<IdentityObject> GetDeliveryObjects(EdgeType edgeType)
		{
			var identityColumnsStr = String.Empty;
			foreach (var identityField in edgeType.Fields.Where(x => x.IsIdentity))
			{
				identityColumnsStr = String.Format("{0}{1},", identityColumnsStr, identityField.IdentityColumnName);
			}
			if (identityColumnsStr.Length == 0) return null;

			identityColumnsStr = identityColumnsStr.Remove(identityColumnsStr.Length - 1, 1);

			var deliveryObjects = new List<IdentityObject>();
			using (var command = new SqlCommand {Connection = _deliverySqlConnection})
			{ 
				command.CommandText = String.Format("SELECT TK, {0} FROM {1} WHERE TYPEID = @typeId",
														identityColumnsStr,
														GetDeliveryTableName(edgeType.TableName));

				command.Parameters.Add(new SqlParameter("@typeId", edgeType.TypeID));
			
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						var deliveryObj = new IdentityObject { TK = reader["TK"].ToString() };
						foreach (var identityName in edgeType.Fields.Where(x => x.IsIdentity).Select(x => x.IdentityColumnName))
						{
							deliveryObj.ObjectIdentities.Add(new IdentityField
							{
								FieldName = identityName,
								Value = reader[identityName].ToString()
							});
						}
						deliveryObjects.Add(deliveryObj);
					}
				}
			}
			return deliveryObjects;
		}

		private string GetDeliveryTableName(string tableName)
		{
			return String.Format("[dbo].[{0}_{1}]", TablePrefix, tableName);
		}

		#endregion
	}
}
