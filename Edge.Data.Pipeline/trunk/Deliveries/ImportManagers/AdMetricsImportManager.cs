using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using Edge.Core.Configuration;
using Edge.Core.Data;
using Edge.Data.Objects;
using Edge.Data.Objects.Reflection;
using Edge.Core.Utilities;
using Edge.Core.Services;


namespace Edge.Data.Pipeline.Importing
{
	/// <summary>
	/// Encapsulates the process of adding ads and ad metrics to the delivery staging database.
	/// </summary>
	public class AdMetricsImportManager : DeliveryImportManager, IDisposable
	{
		#region Consts
		public class Consts
		{
			public static class DeliveryHistoryParameters
			{
				public const string TablePerfix = "TablePerfix";
				public const string MeasureNamesSql = "MeasureNamesSql";
				public const string MeasureOltpFieldsSql = "MeasureOltpFieldsSql";
			}

			public static class AppSettings
			{
				public const string Delivery_SqlDb = "Sql.DeliveriesDb";
				public const string Delivery_RollbackCommand = "Sql.RollbackSqlCommand";
			}
		}
		#endregion

		#region Table structure
		/*=========================*/

		private static class Tables
		{
			public static class Ad
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid", size: 100, nullable: false);
				public static ColumnDef Name = new ColumnDef("Name", size: 100);
				public static ColumnDef OriginalID = new ColumnDef("OriginalID", size: 100);
				public static ColumnDef DestinationUrl = new ColumnDef("DestinationUrl", size: 4000);
				public static ColumnDef Campaign_Account_ID = new ColumnDef("Campaign_Account_ID", type: SqlDbType.Int, nullable: false);
				public static ColumnDef Campaign_Account_OriginalID = new ColumnDef("Campaign_Account_OriginalID", type: SqlDbType.NVarChar, size: 100, nullable: false);
				public static ColumnDef Campaign_Channel = new ColumnDef("Campaign_Channel", type: SqlDbType.Int, nullable: false);
				public static ColumnDef Campaign_Name = new ColumnDef("Campaign_Name", size: 100, nullable: false);
				public static ColumnDef Campaign_OriginalID = new ColumnDef("Campaign_OriginalID", size: 100, nullable: false);
				public static ColumnDef Campaign_Status = new ColumnDef("Campaign_Status", type: SqlDbType.Int);
				public static ColumnDef ExtraFieldX = new ColumnDef("ExtraField{0}", type: SqlDbType.NVarChar, copies: 6, size: 4000);
			}

			public static class AdCreative
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid", size: 100, nullable: false);
				public static ColumnDef OriginalID = new ColumnDef("OriginalID", size: 100);
				public static ColumnDef Name = new ColumnDef("Name", size: 100);
				public static ColumnDef CreativeType = new ColumnDef("CreativeType", type: SqlDbType.Int);
				public static ColumnDef FieldX = new ColumnDef("Field{0}", type: SqlDbType.NVarChar, size: 4000, copies: 4);

				/// <summary>
				/// Reserved for post-processing
				/// </summary>
				public static ColumnDef CreativeGK = new ColumnDef("CreativeGK", size: 50, nullable: true);

				/// <summary>
				/// Reserved for post-processing
				/// </summary>
				public static ColumnDef PpcCreativeGK = new ColumnDef("PpcCreativeGK", size: 50, nullable: true);
			}

			public static class AdTarget
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid", size: 100, nullable: false);
				public static ColumnDef OriginalID = new ColumnDef("OriginalID", size: 100);
				public static ColumnDef TargetType = new ColumnDef("TargetType", type: SqlDbType.Int);
				public static ColumnDef DestinationUrl = new ColumnDef("DestinationUrl", size: 4000);
				public static ColumnDef FieldX = new ColumnDef("Field{0}", type: SqlDbType.NVarChar, size: 4000, copies: 4);
				public static ColumnDef ExtraFieldX = new ColumnDef("ExtraField{0}", type: SqlDbType.NVarChar, copies: 6, size: 4000);
			}

			// TODO: flatten
			public static class AdSegment
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid", size: 100, nullable: false);
				public static ColumnDef SegmentID = new ColumnDef("SegmentID", type: SqlDbType.Int, nullable: false);
				public static ColumnDef ValueOriginalID = new ColumnDef("ValueOriginalID", size: 4000);
				public static ColumnDef Value = new ColumnDef("Value", size: 4000);
			}

			public static class Metrics
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid", size: 100, nullable: false);
				public static ColumnDef MetricsUnitGuid = new ColumnDef("MetricsUnitGuid", size: 300, nullable: false);
				public static ColumnDef DownloadedDate = new ColumnDef("DownloadedDate", type: SqlDbType.DateTime, nullable: true, defaultValue: "GetDate()");
				public static ColumnDef TargetPeriodStart = new ColumnDef("TargetPeriodStart", type: SqlDbType.DateTime, nullable: false);
				public static ColumnDef TargetPeriodEnd = new ColumnDef("TargetPeriodEnd", type: SqlDbType.DateTime, nullable: false);
				public static ColumnDef Currency = new ColumnDef("Currency", size: 10);
			}

			public static class MetricsTargetMatch
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid", size: 100, nullable: false);
				public static ColumnDef MetricsUnitGuid = new ColumnDef("MetricsUnitGuid", size: 300, nullable: false);
				public static ColumnDef OriginalID = new ColumnDef("OriginalID", size: 100);
				public static ColumnDef TargetType = new ColumnDef("TargetType", type: SqlDbType.Int);
				public static ColumnDef DestinationUrl = new ColumnDef("DestinationUrl", size: 4000);
				public static ColumnDef FieldX = new ColumnDef("Field{0}", type: SqlDbType.NVarChar, size: 4000, copies: 4);
				public static ColumnDef ExtraFieldX = new ColumnDef("ExtraField{0}", type: SqlDbType.NVarChar, copies: 6, size: 4000);

				/// <summary>
				/// Reserved for post-processing
				/// </summary>
				public static ColumnDef KeywordGK = new ColumnDef("KeywordGK", type: SqlDbType.NChar, size: 50, nullable: true);
				
				/// <summary>
				/// Reserved for post-processing
				/// </summary>
				public static ColumnDef PpcKeywordGK = new ColumnDef("PpcKeywordGK", type: SqlDbType.NChar, size: 50, nullable: true);
			}

			static Dictionary<Type, ColumnDef[]> _columns = new Dictionary<Type, ColumnDef[]>();
			public static ColumnDef[] GetColumns<T>(bool expandCopies = true)
			{
				return GetColumns(typeof(T), expandCopies);
			}

			public static ColumnDef[] GetColumns(Type type, bool expandCopies = true)
			{
				ColumnDef[] columns;
				lock (_columns)
				{
					if (_columns.TryGetValue(type, out columns))
						return columns;

					FieldInfo[] fields = type.GetFields(BindingFlags.Static | BindingFlags.Public);
					columns = new ColumnDef[fields.Length];
					for (int i = 0; i < fields.Length; i++)
					{
						columns[i] = (ColumnDef)fields[i].GetValue(null);
					}
					_columns.Add(type, columns);
				}

				if (expandCopies)
				{
					var expanded = new List<ColumnDef>(columns.Length);
					foreach (ColumnDef col in columns)
					{
						if (col.Copies <= 1)
						{
							expanded.Add(col);
						}
						else
						{
							for (int i = 1; i <= col.Copies; i++)
								expanded.Add(new ColumnDef(col, i));
						}

					}
					columns = expanded.ToArray();
				}

				return columns;
			}
		}

		/*=========================*/
		#endregion

		#region Supporting classes
		/*=========================*/

		struct ColumnDef
		{
			public string Name;
			public SqlDbType Type;
			public int Size;
			public bool Nullable;
			public int Copies;
			public string DefaultValue;


			public ColumnDef(string name, int size = 0, SqlDbType type = SqlDbType.NVarChar, bool nullable = true, int copies = 1, string defaultValue = "")
			{
				this.Name = name;
				this.Type = type;
				this.Size = size;
				this.Nullable = nullable;
				this.Copies = copies;
				this.DefaultValue = defaultValue;

				if (copies < 1)
					throw new ArgumentException("Column copies cannot be less than 1.", "copies");
				if (copies > 1 && this.Name.IndexOf("{0}") < 0)
					throw new ArgumentException("If copies is bigger than 1, name must include a formattable placholder.", "name");
			}

			public ColumnDef(ColumnDef copySource, int index)
				: this(
					name: String.Format(copySource.Name, index),
					size: copySource.Size,
					type: copySource.Type,
					nullable: copySource.Nullable,
					copies: 1
					)
			{
			}
		}


		class BulkObjects : IDisposable
		{
			public readonly static int BufferSize = int.Parse(AppSettings.Get(typeof(AdMetricsImportManager), "BufferSize"));

			public SqlConnection Connection;
			public List<ColumnDef> Columns;
			public DataTable Table;
			public SqlBulkCopy BulkCopy;

			public BulkObjects(string tablePrefix, Type tableDefinition, SqlConnection connection)
			{
				string tbl = tablePrefix + "_" + tableDefinition.Name;
				this.Columns = new List<ColumnDef>(Tables.GetColumns(tableDefinition, true));

				// Create the table used for bulk insert
				this.Table = new DataTable(tbl);
				foreach (ColumnDef col in this.Columns)
				{
					var tableCol = new DataColumn(col.Name);
					tableCol.AllowDBNull = col.Nullable;
					if (col.Size != 0)
						tableCol.MaxLength = col.Size;
					this.Table.Columns.Add(tableCol);
				}

				// Create the bulk insert operation
				this.BulkCopy = new SqlBulkCopy(connection);
				this.BulkCopy.DestinationTableName = tbl;
				foreach (ColumnDef col in this.Columns)
					this.BulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(col.Name, col.Name));
			}
			public void AddColumn(ColumnDef columnDef)
			{
				this.Columns.Add(columnDef);
				var tableCol = new DataColumn(columnDef.Name);
				tableCol.AllowDBNull = columnDef.Nullable;
				if (columnDef.Size != 0)
					tableCol.MaxLength = columnDef.Size;
				this.Table.Columns.Add(tableCol);
				this.BulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(columnDef.Name, columnDef.Name));
			}

			public void SubmitRow(Dictionary<ColumnDef, object> values)
			{
				DataRow row = this.Table.NewRow();
				foreach (KeyValuePair<ColumnDef, object> col in values)
				{
					row[col.Key.Name] = DataManager.Normalize(col.Value);
				}

				this.Table.Rows.Add(row);

				// Auto flush
				if (this.Table.Rows.Count >= BufferSize)
					this.Flush();
			}

			public string GetCreateTableSql()
			{
				StringBuilder builder = new StringBuilder();
				builder.AppendFormat("create table [dbo].{0} (\n", this.Table.TableName);
				for (int i = 0; i < this.Columns.Count; i++)
				{
					ColumnDef col = this.Columns[i];
					builder.AppendFormat("\t[{0}] [{1}] {2} {3} {4}, \n",
						col.Name,
						col.Type,
						col.Size != 0 ? string.Format("({0})", col.Size) : null,
						col.Nullable ? "null" : "not null",
						col.DefaultValue != string.Empty ? string.Format("Default {0}", col.DefaultValue) : string.Empty
					);
				}
				builder.Remove(builder.Length - 1, 1);
				builder.Append(");");

				string cmdText = builder.ToString();
				return cmdText;
				//SqlCommand cmd = new SqlCommand(cmdText, this.Connection);
				//cmd.ExecuteNonQuery();
			}

			public string GetCreateIndexSql()
			{
				throw new NotImplementedException();
			}

			public void Flush()
			{
				this.BulkCopy.WriteToServer(this.Table);
				this.Table.Clear();
			}

			public void Dispose(bool flush)
			{
				if (flush)
					this.Flush();
				this.BulkCopy.Close();
			}

			public void Dispose()
			{
				this.Dispose(false);
			}

		}

		/*=========================*/
		#endregion

		#region Fields
		/*=========================*/

		private BulkObjects _bulkAd;
		private BulkObjects _bulkAdSegment;
		private BulkObjects _bulkAdTarget;
		private BulkObjects _bulkAdCreative;
		private BulkObjects _bulkMetrics;
		private BulkObjects _bulkMetricsTargetMatch;

		private SqlConnection _sqlConnection;

		public Func<Ad, long> OnAdUsidRequired = null;

		public string TablePrefix { get; private set; }
		public Dictionary<string, Measure> Measures { get; private set; }


		public AdMetricsImportManager(long serviceInstanceID):base(serviceInstanceID)
		{
		}


		/*=========================*/
		#endregion

		#region Import
		/*=========================*/
	
		protected override void OnBeginImport()
		{
			this.TablePrefix = string.Format("D{0}_{1}_{2}", this.CurrentDelivery.Account.ID, DateTime.Now.ToString("yyyMMdd_hhmmss"), this.CurrentDelivery.DeliveryID.ToString("N").ToLower());
			this.HistoryEntryParameters.Add(Consts.DeliveryHistoryParameters.TablePerfix, this.TablePrefix);
			
			// Connect to database
			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();

			_bulkAd = new BulkObjects(this.TablePrefix, typeof(Tables.Ad), _sqlConnection);
			_bulkAdSegment = new BulkObjects(this.TablePrefix, typeof(Tables.AdSegment), _sqlConnection);
			_bulkAdTarget = new BulkObjects(this.TablePrefix, typeof(Tables.AdTarget), _sqlConnection);
			_bulkAdCreative = new BulkObjects(this.TablePrefix, typeof(Tables.AdCreative), _sqlConnection);
			_bulkMetrics = new BulkObjects(this.TablePrefix, typeof(Tables.Metrics), _sqlConnection);
			_bulkMetricsTargetMatch = new BulkObjects(this.TablePrefix, typeof(Tables.MetricsTargetMatch), _sqlConnection);

			// Get measures
			using (SqlConnection oltpConnection = new SqlConnection(AppSettings.GetConnectionString(this, "Oltp")))
			{
				oltpConnection.Open();

				this.Measures = Measure.GetMeasures(
					this.CurrentDelivery.Account,
					this.CurrentDelivery.Channel,
					oltpConnection,
						// NOT IsTarget and NOT IsCalculated and NOT IsBO
						MeasureOptions.IsTarget | MeasureOptions.IsCalculated | MeasureOptions.IsBackOffice,
						MeasureOptionsOperator.Not
					);
			}

			// Add measure columns to metrics,create measuresFieldNamesSQL,measuresNamesSQL
			StringBuilder measuresFieldNamesSQL = new StringBuilder();
			StringBuilder measuresNamesSQL = new StringBuilder();
			int count = 0;
			foreach (Measure  measure in this.Measures.Values)
			{
				_bulkMetrics.AddColumn(new ColumnDef(
					name: measure.Name,
					type: SqlDbType.Float,
					nullable: true
					));

				measuresFieldNamesSQL.AppendFormat("{0}{1}",measure.OltpName, count < this.Measures.Values.Count-1 ? "," : null);
				measuresNamesSQL.AppendFormat("{0}{1}", measure.Name, count < this.Measures.Values.Count - 1 ? "," : null);
				count++;
			}

			this.HistoryEntryParameters.Add(Consts.DeliveryHistoryParameters.MeasureOltpFieldsSql, measuresFieldNamesSQL.ToString());
			this.HistoryEntryParameters.Add(Consts.DeliveryHistoryParameters.MeasureNamesSql, measuresNamesSQL.ToString());

			// Create the tables
			StringBuilder createTableCmdText = new StringBuilder();
			createTableCmdText.Append(_bulkAd.GetCreateTableSql());
			createTableCmdText.Append(_bulkAdSegment.GetCreateTableSql());
			createTableCmdText.Append(_bulkAdTarget.GetCreateTableSql());
			createTableCmdText.Append(_bulkAdCreative.GetCreateTableSql());
			createTableCmdText.Append(_bulkMetrics.GetCreateTableSql());
			createTableCmdText.Append(_bulkMetricsTargetMatch.GetCreateTableSql());
			SqlCommand cmd = new SqlCommand(createTableCmdText.ToString(), _sqlConnection);
			cmd.ExecuteNonQuery();

		}

		public void ImportAd(Ad ad)
		{
			if (this.State != DeliveryImportManagerState.Importing)
				throw new InvalidOperationException("BeginImport must be called before anything can be imported.");

			string adUsid = GetAdIdentity(ad);

			// Ad
			var adRow = new Dictionary<ColumnDef, object>()
			{
				{Tables.Ad.AdUsid, adUsid},
				{Tables.Ad.Name, ad.Name},
				{Tables.Ad.OriginalID, ad.OriginalID},
				{Tables.Ad.DestinationUrl, ad.DestinationUrl},
				{Tables.Ad.Campaign_Account_ID, ad.Campaign.Account.ID},
				{Tables.Ad.Campaign_Account_OriginalID, ad.Campaign.Account.OriginalID},
				{Tables.Ad.Campaign_Channel, ad.Campaign.Channel.ID},
				{Tables.Ad.Campaign_Name, ad.Campaign.Name},
				{Tables.Ad.Campaign_OriginalID, ad.Campaign.OriginalID},
				{Tables.Ad.Campaign_Status, ad.Campaign.Status},
			};
			foreach (KeyValuePair<ExtraField, object> extraField in ad.ExtraFields)
				adRow[new ColumnDef(Tables.AdTarget.ExtraFieldX, extraField.Key.ColumnIndex)] = extraField.Value;

			_bulkAd.SubmitRow(adRow);

			// AdTarget
			foreach (Target target in ad.Targets)
			{
				var row = new Dictionary<ColumnDef, object>()
				{
					{ Tables.AdTarget.AdUsid, adUsid },
					{ Tables.AdTarget.OriginalID, target.OriginalID },
					{ Tables.AdTarget.DestinationUrl, target.DestinationUrl },
					{ Tables.AdTarget.TargetType, target.TypeID }
				};

				foreach (KeyValuePair<MappedFieldMetadata, object> fixedField in target.GetFieldValues())
					row[new ColumnDef(Tables.AdTarget.FieldX, fixedField.Key.ColumnIndex)] = fixedField.Value;

				foreach (KeyValuePair<ExtraField, object> extraField in target.ExtraFields)
					row[new ColumnDef(Tables.AdTarget.ExtraFieldX, extraField.Key.ColumnIndex)] = extraField.Value;

				_bulkAdTarget.SubmitRow(row);
			}

			// AdCreative
			foreach (Creative creative in ad.Creatives)
			{
				var row = new Dictionary<ColumnDef, object>()
				{
					{ Tables.AdCreative.AdUsid, adUsid },
					{ Tables.AdCreative.OriginalID, creative.OriginalID },
					{ Tables.AdCreative.Name, creative.Name },
					{ Tables.AdCreative.CreativeType, creative.TypeID }
				};

				foreach (KeyValuePair<MappedFieldMetadata, object> fixedField in creative.GetFieldValues())
					row[new ColumnDef(Tables.AdTarget.FieldX, fixedField.Key.ColumnIndex)] = fixedField.Value;

				_bulkAdCreative.SubmitRow(row);
			}

			// AdSegment
			foreach (KeyValuePair<Segment, SegmentValue> segment in ad.Segments)
			{
				_bulkAdSegment.SubmitRow(new Dictionary<ColumnDef, object>()
				{
					{ Tables.AdSegment.AdUsid, adUsid },
					{ Tables.AdSegment.SegmentID, segment.Key.ID },
					{ Tables.AdSegment.Value, segment.Value.Value },
					{ Tables.AdSegment.ValueOriginalID, segment.Value.OriginalID }
				});
			}
		}


		public void ImportMetrics(AdMetricsUnit metrics)
		{
			if (this.State != DeliveryImportManagerState.Importing)
				throw new InvalidOperationException("BeginImport must be called before anything can be imported.");

			if (metrics.Ad == null)
				throw new InvalidOperationException("Cannot import a metrics unit that is not associated with an ad.");

			string adUsid = GetAdIdentity(metrics.Ad);

			// Metrics
			var metricsRow = new Dictionary<ColumnDef, object>()
			{
				{Tables.Metrics.MetricsUnitGuid, metrics.Guid.ToString("N")},
				{Tables.Metrics.AdUsid, adUsid},
				{Tables.Metrics.TargetPeriodStart, metrics.PeriodStart},
				{Tables.Metrics.TargetPeriodEnd, metrics.PeriodEnd},
				{Tables.Metrics.Currency, metrics.Currency == null ? null : metrics.Currency.Code}
			};

			foreach (KeyValuePair<Measure, double> measure in metrics.MeasureValues)
			{
				// Use the Oltp name of the measure as the column name
				metricsRow[new ColumnDef(measure.Key.Name)] = measure.Value;
			}

			_bulkMetrics.SubmitRow(metricsRow);

			// MetricsTargetMatch
			// TODO: this shouldn't just duplicate ad targets - find a different solution
			foreach (Target target in metrics.TargetMatches)
			{
				var row = new Dictionary<ColumnDef, object>()
				{
					{ Tables.MetricsTargetMatch.MetricsUnitGuid, metricsRow[Tables.Metrics.MetricsUnitGuid] },
					{ Tables.MetricsTargetMatch.AdUsid, adUsid },
					{ Tables.MetricsTargetMatch.OriginalID, target.OriginalID },
					{ Tables.MetricsTargetMatch.DestinationUrl, target.DestinationUrl },
					{ Tables.MetricsTargetMatch.TargetType, target.TypeID }
				};

				foreach (KeyValuePair<MappedFieldMetadata, object> fixedField in target.GetFieldValues())
					row[new ColumnDef(Tables.AdTarget.FieldX, fixedField.Key.ColumnIndex)] = fixedField.Value;

				foreach (KeyValuePair<ExtraField, object> customField in target.ExtraFields)
					row[new ColumnDef(Tables.AdTarget.ExtraFieldX, customField.Key.ColumnIndex)] = customField.Value;

				_bulkMetricsTargetMatch.SubmitRow(row);
			}

		}



		private string GetAdIdentity(Ad ad)
		{
			string val;
			if (this.OnAdUsidRequired != null)
				val = this.OnAdUsidRequired(ad).ToString();
			else if (String.IsNullOrEmpty(ad.OriginalID))
				throw new Exception("Ad.OriginalID is required. If it is not available, provide a function for AdDataImportSession.OnAdIdentityRequired that returns a unique value for this ad.");
			else
				val = ad.OriginalID.ToString();

			return val;
		}

		protected void OnEndImport()
		{
		}

		/*=========================*/
		#endregion

		#region Commit
		/*=========================*/

		SqlTransaction _transaction = null;
		SqlCommand _prepareCommand = null;

		protected override void OnBeginCommit()
		{
			_sqlConnection = NewDeliveryDbConnection();
			_sqlConnection.Open();

			_transaction = _sqlConnection.BeginTransaction("Delivery Commit");
		}

		protected override void OnCommit()
		{
			string prepareCmdText;
			if (!Service.Current.Instance.Configuration.Options.TryGetValue("PrepareSqlCommand", out prepareCmdText))
				throw new InvalidOperationException(string.Format("Configuration option required: {0}", "PrepareSqlCommand"));
			string commitCmdText;
			if (!this.Instance.Configuration.Options.TryGetValue("CommitSqlCommand", out commitCmdText))
				throw new InvalidOperationException(string.Format("Configuration option required: {0}", "CommitSqlCommand"));

			DeliveryHistoryEntry processedEntry = this.Delivery.History.Last(entry => entry.Operation == DeliveryOperation.Imported);
			if (processedEntry == null)
				throw new Exception("This delivery has not been processed yet (could not find a 'processed' history entry).");

			DeliveryHistoryEntry commitEntry = new DeliveryHistoryEntry(DeliveryOperation.Comitted, this.Instance.InstanceID, new Dictionary<string, object>());

			// get this from last 'Processed' history entry
			string measuresFieldNamesSQL = processedEntry.Parameters[Consts.DeliveryHistoryParameters.MeasureOltpFieldsSql].ToString();
			string measuresNamesSQL = processedEntry.Parameters[Consts.DeliveryHistoryParameters.MeasureNamesSql].ToString();
			string tablePerfix = processedEntry.Parameters[Consts.DeliveryHistoryParameters.TablePerfix].ToString();
			string deliveryId = this.Delivery.DeliveryID.ToString("N");
			string commitTableName;

			// ...........................
			// FINALIZE data
			using (SqlConnection connection = new SqlConnection(AppSettings.GetConnectionString(typeof(Delivery), Consts.AppSettings.Delivery_SqlDb)))
			{
				connection.Open();
				using (SqlCommand command = DataManager.CreateCommand(prepareCmdText, CommandType.StoredProcedure))
				{
					command.Connection = connection;

					command.Parameters["@DeliveryID"].Size = 4000;
					command.Parameters["@DeliveryID"].Value = deliveryId;

					command.Parameters["@DeliveryTablePrefix"].Size = 4000;
					command.Parameters["@DeliveryTablePrefix"].Value = tablePerfix;

					command.Parameters["@MeasuresNamesSQL"].Size = 4000;
					command.Parameters["@MeasuresNamesSQL"].Value = measuresNamesSQL;

					command.Parameters["@MeasuresFieldNamesSQL"].Size = 4000;
					command.Parameters["@MeasuresFieldNamesSQL"].Value = measuresFieldNamesSQL;

					command.Parameters["@CommitTableName"].Size = 4000;

					command.ExecuteNonQuery();

					commitEntry.Parameters["CommitTableName"] = commitTableName = command.Parameters["@CommitTableName"].Value.ToString();

				}
			}

			// ...........................
			// WAIT FOR ROLLBACK TO END
			// If there's a rollback going on, wait for it to end (will throw exceptions if error occured)
			if (rollbackOperation != null)
			{
				rollbackOperation.Wait();
			}

			// ...........................
			// COMMIT data to OLTP
			using (SqlConnection connection = new SqlConnection(AppSettings.GetConnectionString(typeof(Delivery), Consts.AppSettings.Delivery_SqlDb)))
			{
				connection.Open();
				using (SqlCommand command = DataManager.CreateCommand(commitCmdText, CommandType.StoredProcedure))
				{
					command.Connection = connection;

					command.Parameters["@DeliveryFileName"].Size = 4000;
					command.Parameters["@DeliveryFileName"].Value = tablePerfix;

					command.Parameters["@CommitTableName"].Size = 4000;
					command.Parameters["@CommitTableName"].Value = commitTableName;

					command.Parameters["@MeasuresNamesSQL"].Size = 4000;
					command.Parameters["@MeasuresNamesSQL"].Value = measuresNamesSQL;

					command.Parameters["@MeasuresFieldNamesSQL"].Size = 4000;
					command.Parameters["@MeasuresFieldNamesSQL"].Value = measuresFieldNamesSQL;

					command.ExecuteNonQuery();
				}
			}

			this.Delivery.History.Add(commitEntry);
			this.Delivery.Save();

			return Core.Services.ServiceOutcome.Success;
		}

		internal static void Rollback(Delivery[] deliveries)
		{
			
		}

		public override void OnRollback()
		{
			string cmdText = AppSettings.Get(this, "Sql.RollbackCommand");
			SqlCommand cmd = DataManager.CreateCommand(cmdText, System.Data.CommandType.StoredProcedure);
			cmd.Connection = _sqlConnection;

			string guid = this.Delivery.DeliveryID.ToString("N");
			DeliveryHistoryEntry commitEntry = this.Delivery.History.Last(entry => entry.Operation == DeliveryOperation.Comitted);
			if (commitEntry == null)
				throw new Exception(String.Format("The delivery '{0}' has never been comitted so it cannot be rolled back.", guid));

			cmd.Parameters["@DeliveryID"].Value = guid;
			cmd.Parameters["@TableName"].Value = commitEntry.Parameters[Consts.DeliveryHistoryParameters.TablePerfix];

			this.Delivery.History.Add(
				DeliveryOperation.RolledBack,
				Service.Current != null ? new long?(Service.Current.Instance.InstanceID) : null);
		}

		public RollbackOperation RollbackConflicting(DeliveryConflictBehavior defaultBehavior, out Delivery newDelivery, bool async)
		{
			DeliveryConflictBehavior behavior = defaultBehavior;
			string configuredBehavior;
			if (Service.Current.Instance.Configuration.Options.TryGetValue("ConflictBehavior", out configuredBehavior))
				behavior = (DeliveryConflictBehavior)Enum.Parse(typeof(DeliveryConflictBehavior), configuredBehavior);

			RollbackOperation operation = null;
			newDelivery = NewDelivery();

			if (behavior != DeliveryConflictBehavior.Ignore)
			{
				Delivery[] conflicting = Delivery.GetConflicting(newDelivery);
				if (conflicting.Length > 0)
				{
					// Check whether the last commit was not rolled back for each conflicting delivery
					List<Delivery> toRollback = new List<Delivery>();
					foreach (Delivery d in conflicting)
					{
						int rollbackIndex = -1;
						int commitIndex = -1;
						for (int i = 0; i < d.History.Count; i++)
						{
							if (d.History[i].Operation == DeliveryOperation.Comitted)
								commitIndex = i;
							else if (d.History[i].Operation == DeliveryOperation.RolledBack)
								rollbackIndex = i;
						}

						if (commitIndex > rollbackIndex)
							toRollback.Add(d);
					}

					if (behavior == DeliveryConflictBehavior.Rollback)
					{
						if (async)
						{
							operation = new RollbackOperation();
							operation.AsyncDelegate = new Action<Delivery[]>(Delivery.Rollback);
							operation.AsyncResult = operation.AsyncDelegate.BeginInvoke(toRollback.ToArray(), null, null);
						}
						else
						{
							Delivery.Rollback(toRollback.ToArray());
						}
					}
					else
					{
						StringBuilder guids = new StringBuilder();
						for (int i = 0; i < conflicting.Length; i++)
						{
							guids.Append(conflicting[i].DeliveryID.ToString("N"));
							if (i < conflicting.Length - 1)
								guids.Append(", ");
						}
						throw new Exception("Conflicting deliveries found: " + guids.ToString());
					}
				}
			}

			return operation;
		}

		/*=========================*/
		#endregion

		SqlConnection NewDeliveryDbConnection()
		{
			return new SqlConnection(AppSettings.GetConnectionString(this, Consts.AppSettings.Delivery_SqlDb));
		}
			
		void IDisposable.Dispose()
		{
			_bulkAd.Dispose(true);
			_bulkAdCreative.Dispose(true);
			_bulkAdTarget.Dispose(true);
			_bulkAdSegment.Dispose(true);
			_bulkMetrics.Dispose(true);
			_bulkMetricsTargetMatch.Dispose(true);

			_sqlConnection.Dispose();
		}


	}

}
