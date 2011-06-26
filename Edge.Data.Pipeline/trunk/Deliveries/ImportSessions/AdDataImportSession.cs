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


namespace Edge.Data.Pipeline.Importing
{
	/// <summary>
	/// Encapsulates the process of adding ads and ad metrics to the delivery staging database.
	/// </summary>
	public class AdDataImportSession : DeliveryImportSession<AdMetricsUnit>, IDisposable
	{
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
			}

			public static class AdCreative
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid", size: 100, nullable: false);
				public static ColumnDef OriginalID = new ColumnDef("OriginalID", size: 100);
				public static ColumnDef Name = new ColumnDef("Name", size: 100);
				public static ColumnDef CreativeType = new ColumnDef("CreativeType", type: SqlDbType.Int);
				public static ColumnDef FieldX = new ColumnDef("Field{0}", type: SqlDbType.NVarChar, size: 4000, copies: 4);
				public static ColumnDef CreativeGK = new ColumnDef("CreativeGK", size: 50, nullable: true);
				public static ColumnDef PPCCreativeGK = new ColumnDef("PPCCreativeGK", size: 50, nullable: true);
			}

			public static class AdTarget
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid", size: 100, nullable: false);
				public static ColumnDef OriginalID = new ColumnDef("OriginalID", size: 100);
				public static ColumnDef TargetType = new ColumnDef("TargetType", type: SqlDbType.Int);
				public static ColumnDef DestinationUrl = new ColumnDef("DestinationUrl", size: 4000);
				public static ColumnDef FieldX = new ColumnDef("Field{0}", type: SqlDbType.NVarChar, size: 4000, copies: 4);
				public static ColumnDef CustomFieldX = new ColumnDef("CustomField{0}", type: SqlDbType.NVarChar, copies: 6, size: 4000);
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
				public static ColumnDef OriginalID = new ColumnDef("OriginalID", size: 100);
				public static ColumnDef TargetType = new ColumnDef("TargetType", type: SqlDbType.Int);
				public static ColumnDef DestinationUrl = new ColumnDef("DestinationUrl", size: 4000);
				public static ColumnDef FieldX = new ColumnDef("Field{0}", type: SqlDbType.NVarChar, size: 4000, copies: 4);
				public static ColumnDef CustomFieldX = new ColumnDef("CustomField{0}", type: SqlDbType.NVarChar, copies: 6, size: 4000);
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
			public readonly static int BufferSize = int.Parse(AppSettings.Get(typeof(AdDataImportSession), "BufferSize"));

			public SqlConnection Connection;
			public List<ColumnDef> Columns;
			public DataTable Table;
			public SqlBulkCopy BulkCopy;

			public BulkObjects(string tablePrefix, Type tableDefinition, SqlConnection connection)
			{
				string tbl = tablePrefix + tableDefinition.Name;
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
					row[col.Key.Name] = Normalize(col.Value);
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

			private static object Normalize(object myObject)
			{
				object returnObject;
				if (myObject == null)
					returnObject = DBNull.Value;
				else
				{
					if (myObject is Enum)
						returnObject = (int)myObject;
					else
						returnObject = myObject;
				}
				return returnObject;
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

		private BulkObjects _bulkAd;
		private BulkObjects _bulkAdSegment;
		private BulkObjects _bulkAdTarget;
		private BulkObjects _bulkAdCreative;
		private BulkObjects _bulkMetrics;
		private BulkObjects _bulkMetricsTargetMatch;

		private SqlConnection _sqlConnection;

		public Func<Ad, long> OnAdIdentityRequired = null;
		public string TablePrefix { get; private set; }
		public Dictionary<string, Measure> Measures { get; private set; }

		public AdDataImportSession(Delivery delivery)
			: base(delivery)
		{
		}

		public override void Begin(bool reset = true)
		{

			this.TablePrefix = string.Format("D{0}_{1}_{2}_", Delivery.Account.ID, DateTime.Now.ToString("yyyMMdd_hhmmss"), Delivery.DeliveryID.ToString("N").ToLower());

			// Connect to database
			try
			{
				_sqlConnection = new SqlConnection(AppSettings.GetConnectionString(this, "DeliveriesDb"));
				_sqlConnection.Open();
			}
			catch (Exception ex)
			{

				Log.Write(string.Format("DeliverisDb: {0} ",ex.Message), ex, LogMessageType.Error);

			}

			_bulkAd = new BulkObjects(this.TablePrefix, typeof(Tables.Ad), _sqlConnection);
			_bulkAdSegment = new BulkObjects(this.TablePrefix, typeof(Tables.AdSegment), _sqlConnection);
			_bulkAdTarget = new BulkObjects(this.TablePrefix, typeof(Tables.AdTarget), _sqlConnection);
			_bulkAdCreative = new BulkObjects(this.TablePrefix, typeof(Tables.AdCreative), _sqlConnection);
			_bulkMetrics = new BulkObjects(this.TablePrefix, typeof(Tables.Metrics), _sqlConnection);
			_bulkMetricsTargetMatch = new BulkObjects(this.TablePrefix, typeof(Tables.MetricsTargetMatch), _sqlConnection);

			// Get measures

			try
			{
				using (SqlConnection oltpConnection = new SqlConnection(AppSettings.GetConnectionString(this, "Oltp")))
				{
					oltpConnection.Open();

					this.Measures = Measure.GetMeasuresForAccount(this.Delivery.Account, MeasureType.All ^ MeasureType.Target ^ MeasureType.Calculated, oltpConnection);
				}
			}
			catch (Exception ex)
			{

				Log.Write(string.Format("Oltp: {0} ", ex.Message), ex, LogMessageType.Error);

			}

			// Add measure columns to metrics
			foreach (Measure measure in this.Measures.Values)
			{
				_bulkMetrics.AddColumn(new ColumnDef(
					name: measure.Name,
					type: SqlDbType.Float,
					nullable: true
					));
			}

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
			string adUsid = GetAdIdentity(ad);

			// Ad
			_bulkAd.SubmitRow(new Dictionary<ColumnDef, object>()
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
			});

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

				foreach (KeyValuePair<ExtraField, object> customField in target.ExtraFields)
					row[new ColumnDef(Tables.AdTarget.CustomFieldX, customField.Key.ColumnIndex)] = customField.Value;

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
					{ Tables.MetricsTargetMatch.AdUsid, adUsid },
					{ Tables.MetricsTargetMatch.OriginalID, target.OriginalID },
					{ Tables.MetricsTargetMatch.DestinationUrl, target.DestinationUrl },
					{ Tables.MetricsTargetMatch.TargetType, target.TypeID }
				};

				foreach (KeyValuePair<MappedFieldMetadata, object> fixedField in target.GetFieldValues())
					row[new ColumnDef(Tables.AdTarget.FieldX, fixedField.Key.ColumnIndex)] = fixedField.Value;

				foreach (KeyValuePair<ExtraField, object> customField in target.ExtraFields)
					row[new ColumnDef(Tables.AdTarget.CustomFieldX, customField.Key.ColumnIndex)] = customField.Value;

				_bulkMetricsTargetMatch.SubmitRow(row);
			}

		}



		private string GetAdIdentity(Ad ad)
		{
			string val;
			if (this.OnAdIdentityRequired != null)
				val = this.OnAdIdentityRequired(ad).ToString();
			else if (String.IsNullOrEmpty(ad.OriginalID))
				throw new Exception("Ad.OriginalID is required. If it is not available, provide a function for AdDataImportSession.OnAdIdentityRequired that returns a unique value for this ad.");
			else
				val = ad.OriginalID.ToString();

			return val;
		}

		public override void Commit()
		{
			throw new NotSupportedException("Committing a session cannot be done from here.");
		}

		public void Dispose()
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
