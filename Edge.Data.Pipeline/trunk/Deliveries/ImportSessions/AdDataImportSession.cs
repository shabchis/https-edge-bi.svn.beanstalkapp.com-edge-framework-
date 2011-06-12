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
				public static ColumnDef AdUsid					= new ColumnDef("AdUsid", size: 100, nullable: false);
				public static ColumnDef Name					= new ColumnDef("Name", size: 100);
				public static ColumnDef OriginalID				= new ColumnDef("OriginalID", size: 100);
				public static ColumnDef DestinationUrl			= new ColumnDef("DestinationUrl", size: 4096);
				public static ColumnDef Campaign_Account		= new ColumnDef("Campaign_Account", type: SqlDbType.Int, nullable: false);
				public static ColumnDef Campaign_Channel		= new ColumnDef("Campaign_Channel", type: SqlDbType.Int, nullable: false);
				public static ColumnDef Campaign_Name			= new ColumnDef("Campaign_Name", size: 100, nullable: false);
				public static ColumnDef Campaign_OriginalID		= new ColumnDef("Campaign_OriginalID", size: 100, nullable: false);
				public static ColumnDef Campaign_Status			= new ColumnDef("Campaign_Status", type: SqlDbType.Int);
			}

			public static class AdCreative
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid");
				public static ColumnDef OriginalID = new ColumnDef("OriginalID");
				public static ColumnDef Name = new ColumnDef("Name");
				public static ColumnDef CreativeType = new ColumnDef("CreativeType", type: SqlDbType.Int);
				public static ColumnDef FieldX = new ColumnDef("Field{0}");
			}

			public static class AdTarget
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid");
				public static ColumnDef OriginalID = new ColumnDef("OriginalID");
				public static ColumnDef TargetType = new ColumnDef("TargetType", type: SqlDbType.Int);
				public static ColumnDef DestinationUrl = new ColumnDef("DestinationUrl");
				public static ColumnDef FieldX = new ColumnDef("Field{0}");
				public static ColumnDef CustomFieldX = new ColumnDef("CustomField{0}");
			}

			public static class AdSegment
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid");
				public static ColumnDef SegmentID = new ColumnDef("SegmentID");
				public static ColumnDef ValueOriginalID = new ColumnDef("ValueOriginalID");
				public static ColumnDef Value = new ColumnDef("Value");
			}

			public static class Metrics
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid");
				public static ColumnDef MetricsUnitGuid = new ColumnDef("MetricsUnitGuid");
				public static ColumnDef TargetPeriodStart = new ColumnDef("TargetPeriodStart");
				public static ColumnDef TargetPeriodEnd = new ColumnDef("TargetPeriodEnd");
				public static ColumnDef Currency = new ColumnDef("Currency");
				public static ColumnDef MeasureID = new ColumnDef("MeasureID");
				public static ColumnDef MeasureValue = new ColumnDef("MeasureValue");
			}

			public static class MetricsTargetMatch
			{
				public static ColumnDef AdUsid = new ColumnDef("AdUsid");
				public static ColumnDef OriginalID = new ColumnDef("OriginalID");
			}

			static Dictionary<Type, ColumnDef[]> _columns = new Dictionary<Type,ColumnDef[]>();
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
						columns[i] = (ColumnDef) fields[i].GetValue(null);
					}
					_columns.Add(type, columns);
				}

				if (expandCopies)
				{
					var expanded = new List<ColumnDef>(columns.Length);
					foreach(ColumnDef col in columns)
					{
						if (col.Size <= 1)
						{
							expanded.Add(col);
						}
						else
						{
							for (int i = 1; i <= col.Size; i++)
								expanded.Add(new ColumnDef(
									name: String.Format(col.Name, i),
									size: col.Size,
									type: col.Type,
									nullable: col.Nullable,
									copies: 1));
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

			public ColumnDef(string name, int size = 0, SqlDbType type = SqlDbType.NVarChar, bool nullable = true, int copies = 1)
			{
				this.Name = name;
				this.Type = type;
				this.Size = size;
				this.Nullable = nullable;
				this.Copies = copies;

				if (copies < 1)
					throw new ArgumentException("Column copies cannot be less than 1.", "copies");
				if (copies > 1 && this.Name.IndexOf("{0}") < 0)
					throw new ArgumentException("If copies is bigger than 1, name must include a formattable placholder.", "name");
			}
		}


		class BulkObjects
		{
			public SqlConnection Connection;
			public ColumnDef[] Columns;
			public DataTable Table;
			public SqlBulkCopy BulkCopy;

			public BulkObjects(string tablePrefix, Type tableDefinition, SqlConnection connection)
			{
				string tbl = tablePrefix + tableDefinition.Name;
				ColumnDef[] columns = Tables.GetColumns(tableDefinition, true);

				// Create the table used for bulk insert
				this.Table = new DataTable(tbl);
				foreach (ColumnDef col in columns)
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
				foreach (ColumnDef col in columns)
					this.BulkCopy.ColumnMappings.Add(new SqlBulkCopyColumnMapping(col.Name, col.Name));
			}

			public void CreateTable()
			{
				StringBuilder builder = new StringBuilder();
				builder.AppendFormat("create table [dbo].{0} (\n", this.Table.TableName);
				for (int i = 0; i < this.Columns.Length; i++)
				{
					ColumnDef col = this.Columns[i];
					builder.AppendFormat("\t[{0}] [{1}] {2} {3} {4}\n",
						col.Name,
						col.Type,
						col.Size != 0 ? string.Format("({0})", col.Size) : null,
						col.Nullable ? "null" : "not null"
					);
				}
				builder.Append(")");

				string cmdText = builder.ToString();
				SqlCommand cmd = new SqlCommand(cmdText, this.Connection);
				cmd.ExecuteNonQuery();
			}

			public void WriteToServer()
			{
				this.BulkCopy.WriteToServer(this.Table);
				this.Table.Clear();
			}
		}

		/*=========================*/
		#endregion
	
		public Func<Ad, long> OnAdIdentityRequired = null;
		
		private BulkObjects _bulkAd;
		private BulkObjects _bulkAdSegment;
		private BulkObjects _bulkAdTarget;
		private BulkObjects _bulkAdCreative;
		private BulkObjects _bulkMetrics;
		private BulkObjects _bulkMetricsTargetMatch;

		private SqlConnection _sqlConnection;
		private string _baseTableName;
		private int _bufferSize;
		private Dictionary<Type, int> _targetTypes;
		private Dictionary<Type, int> _creativeType;

		public AdDataImportSession(Delivery delivery) : base(delivery)
		{
			_bufferSize = int.Parse(AppSettings.Get(this, "BufferSize"));
		}

		public override void Begin(bool reset = true)
		{
			_baseTableName = string.Format("D{0}_{1}_{2}_", Delivery.Account.ID, DateTime.Today.ToString("yyyMMdd_hhmmss"), Delivery._guid.ToString("N").ToLower());
			
			//initalize connection
			_sqlConnection = new SqlConnection(AppSettings.GetConnectionString(this, "DeliveriesDb"));
			_sqlConnection.Open();

			_bulkAd = new BulkObjects(_baseTableName, typeof(Tables.Ad), _sqlConnection);
			_bulkAdSegment = new BulkObjects(_baseTableName, typeof(Tables.AdSegment), _sqlConnection);
			_bulkAdTarget = new BulkObjects(_baseTableName, typeof(Tables.AdTarget), _sqlConnection);
			_bulkAdCreative = new BulkObjects(_baseTableName, typeof(Tables.AdCreative), _sqlConnection);
			_bulkMetrics = new BulkObjects(_baseTableName, typeof(Tables.Metrics), _sqlConnection);
			_bulkMetricsTargetMatch = new BulkObjects(_baseTableName, typeof(Tables.MetricsTargetMatch), _sqlConnection);

			// Create the tables
			_bulkAd.CreateTable();
			_bulkAdSegment.CreateTable();
			_bulkAdTarget.CreateTable();
			_bulkAdCreative.CreateTable();
			_bulkMetrics.CreateTable();
			_bulkMetricsTargetMatch.CreateTable();
		}

		public void ImportAd(Ad ad)
		{
			string adUsid = GetAdIdentity(ad);

			DataRow row = _adDataTable.NewRow();

			row[AdUsid] = Normalize(adUsid);
			row[Ad_Name] = Normalize(ad.Name);
			row[Ad_OriginalID] = Normalize(ad.OriginalID);
			row[Ad_DestinationUrl] = Normalize(ad.DestinationUrl);
			row[Ad_Campaign_Account] = Normalize(ad.Campaign.Account.ID);
			row[Ad_Campaign_Channel] = Normalize(ad.Campaign.Channel.ID);
			row[Ad_Campaign_Name] = Normalize(ad.Campaign.Name);
			row[Ad_Campaign_OriginalID] = Normalize(ad.Campaign.OriginalID);
			row[Ad_Campaign_Status] = Normalize(((int)ad.Campaign.Status).ToString());



			_adDataTable.Rows.Add(row);
			if (_adDataTable.Rows.Count == _bufferSize)
			{
				_bulkAd.WriteToServer(_adDataTable);
				_adDataTable.Clear();
			}
			//Targets
			foreach (Target target in ad.Targets)
			{
				row = _adTargetDataTable.NewRow();
				row[AdUsid] = adUsid;
				row[Ad_OriginalID] = Normalize(target.OriginalID);
				row[Ad_DestinationUrl] = Normalize(target.DestinationUrl);
				int targetType = GetTargetType(target.GetType());
				row[Ad_TargetType] = Normalize(targetType);
				foreach (FieldInfo field in target.GetType().GetFields())//TODO: GET FILEDS ONLY ONE TIME
				{
					if (Attribute.IsDefined(field, typeof(TargetFieldIndexAttribute)))
					{
						TargetFieldIndexAttribute TargetColumn = (TargetFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(TargetFieldIndexAttribute));
						row[string.Format(FieldX, TargetColumn.TargetColumnIndex)] = Normalize(field.GetValue(target));
					}



				}
				foreach (KeyValuePair<TargetCustomField, object> customField in target.CustomFields)
				{
					row[string.Format(Target_CustomField_Name, customField.Key.FieldIndex)] = Normalize(customField.Value);
				}
				_adTargetDataTable.Rows.Add(row);
				if (_metricsTargetMatchDataTable.Rows.Count == _bufferSize)
				{
					_bulkAdTarget.WriteToServer(_adTargetDataTable);
					_adTargetDataTable.Clear();
				}

			}

			//Creatives
			foreach (Creative creative in ad.Creatives)
			{
				row = _adCreativesDataTable.NewRow();
				row[AdUsid] = Normalize(adUsid);
				row[Ad_OriginalID] = Normalize(creative.OriginalID);
				row[Ad_Name] = Normalize(creative.Name);
				int creativeType = GetCreativeType(creative.GetType());
				row[Ad_CreativeType] = Normalize(creativeType);
				foreach (FieldInfo field in creative.GetType().GetFields())
				{
					if (Attribute.IsDefined(field, typeof(CreativeFieldIndexAttribute)))
					{
						CreativeFieldIndexAttribute creativeColumn = (CreativeFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(CreativeFieldIndexAttribute));
						row[string.Format(FieldX, creativeColumn.CreativeFieldIndex)] = Normalize(field.GetValue(creative));
					}
				}
				_adCreativesDataTable.Rows.Add(row);
				if (_adCreativesDataTable.Rows.Count == _bufferSize)
				{
					_bulkAdCreative.WriteToServer(_adCreativesDataTable);
					_adCreativesDataTable.Clear();
				}
			}
			//segments


			foreach (KeyValuePair<Segment, SegmentValue> Segment in ad.Segments)
			{
				row = _segmetsDataTable.NewRow();
				row[AdUsid] = adUsid;
				row[Segments_SegmentID] = Segment.Key.ID;
				row[Segments_Value] = Segment.Value.Value;
				row[Segments_ValueOriginalID] =Normalize( Segment.Value.OriginalID);
				_segmetsDataTable.Rows.Add(row);
				if (_segmetsDataTable.Rows.Count == _bufferSize)
				{
					_bulkAdSegment.WriteToServer(_segmetsDataTable);
					_segmetsDataTable.Clear();
				}
			}
		}


		public void ImportMetrics(AdMetricsUnit metrics)
		{
			string adUsid = "-1";
			metrics.Guid = Guid.NewGuid();
			DataRow row;

			foreach (KeyValuePair<Measure, double> measure in metrics.Measures)
			{


				row = _metricsDataTable.NewRow();

				row[MetricsUnit_Guid] = metrics.Guid.ToString("N");
				if (metrics.Ad != null)
					adUsid = GetAdIdentity(metrics.Ad);
				row[AdUsid] = Normalize(adUsid);
				row[Metrics_TargetPeriodStart] = metrics.PeriodStart;
				row[Metrics_TargetPeriodEnd] = metrics.PeriodEnd;
				if (metrics.Currency != null)
					row[Metrics_Currency] = Normalize(metrics.Currency.Code);


				//Measures
				row[Metrics_MeasureID] = Normalize(measure.Key.ID);
				row[Metrics_MeasureValue] = Normalize(measure.Value);






				_metricsDataTable.Rows.Add(row);
				if (_metricsDataTable.Rows.Count == _bufferSize)
				{
					_bulkMetrics.WriteToServer(_metricsDataTable);
					_metricsDataTable.Rows.Clear();
				}
			}

			//tagetmatches
			foreach (Target target in metrics.TargetMatches)
			{
				row = _metricsTargetMatchDataTable.NewRow();
				row[AdUsid] = Normalize(adUsid);
				row[Ad_OriginalID] = Normalize(target.OriginalID);
				row[Ad_DestinationUrl] = Normalize(target.DestinationUrl);
				int targetType = GetTargetType(target.GetType());
				row[Ad_TargetType] = Normalize(targetType);
				foreach (FieldInfo field in target.GetType().GetFields())
				{
					if (Attribute.IsDefined(field, typeof(TargetFieldIndexAttribute)))
					{
						TargetFieldIndexAttribute TargetColumn = (TargetFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(TargetFieldIndexAttribute));
						row[string.Format(FieldX, TargetColumn.TargetColumnIndex)] = Normalize(field.GetValue(target));
					}


				}
				foreach (KeyValuePair<TargetCustomField, object> customField in target.CustomFields)
				{
					row[string.Format(Target_CustomField_Name, customField.Key.FieldIndex)] = Normalize(customField.Value);
				}
				_metricsTargetMatchDataTable.Rows.Add(row);
				if (_metricsTargetMatchDataTable.Rows.Count == _bufferSize)
				{
					_bulkMetricsTargetMatch.WriteToServer(_metricsTargetMatchDataTable);
					_metricsTargetMatchDataTable.Clear();

				}
			}

		}


		private object Normalize(object myObject)
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

		private int GetTargetType(Type type)
		{
			int targetType = -1;
			if (_targetTypes == null)
				_targetTypes = new Dictionary<Type, int>();
			if (_targetTypes.ContainsKey(type))
				targetType = _targetTypes[type];
			else
			{
				if (Attribute.IsDefined(type, typeof(TargetTypeIDAttribute)))
				{
					targetType = ((TargetTypeIDAttribute)Attribute.GetCustomAttribute(type, typeof(TargetTypeIDAttribute))).TargetTypeID;
					_targetTypes.Add(type, targetType);
				}
				else
					throw new Exception("Mapping Probem, targettype attribute is not defined");
			}
			return targetType;
		}

		private int GetCreativeType(Type type)
		{
			int creativeType = -1;
			if (_creativeType == null)
				_creativeType = new Dictionary<Type, int>();
			if (_creativeType.ContainsKey(type))
				creativeType = _creativeType[type];
			else
			{
				if (Attribute.IsDefined(type, typeof(CreativeTypeIDAttribute)))
				{
					creativeType = ((CreativeTypeIDAttribute)Attribute.GetCustomAttribute(type, typeof(CreativeTypeIDAttribute))).CreativeTypeID;
					_creativeType.Add(type, creativeType);
				}
				else
					throw new Exception("Mapping Probem, targettype attribute is not defined");
			}
			return creativeType;
		}

		public override void Commit()
		{
			throw new NotSupportedException("Committing a session cannot be done from here.");
		}

		public void Dispose()
		{
			_bulkAd.WriteToServer();
			_bulkAdCreative.WriteToServer();
			_bulkAdTarget.WriteToServer();
			_bulkAdSegment.WriteToServer();
			_bulkMetrics.WriteToServer();
			_bulkMetricsTargetMatch.WriteToServer();

			_sqlConnection.Dispose();
		}
	}
}
