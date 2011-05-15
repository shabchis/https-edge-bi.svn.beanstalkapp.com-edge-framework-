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

	public class AdDataImportSession : DeliveryImportSession<AdMetricsUnit>, IDisposable
	{


		private SqlBulkCopy _bulkMetrics;
		private DataTable _metricsDataTable;
		private SqlBulkCopy _bulkMetricsTargetMatch;
		private DataTable _adTargetDataTable;
		private SqlBulkCopy _bulkAdTarget;
		private DataTable _metricsTargetMatchDataTable;
		private SqlBulkCopy _bulkAdCreatives;
		private DataTable _adCreativesDataTable;
		private SqlBulkCopy _bulkAd;
		private DataTable _adDataTable;
		private SqlConnection _sqlConnection;
		private string _baseTableName;
		private int _bufferSize;
		private Dictionary<Type, int> _targetTypes;
		private Dictionary<Type, int> _creativeType;
		#region consts fields
		public const string ads_CreativeType_FieldName = "CreativeType";
		public const string FieldX_FiledName = "Field{0}";
		public const string ads_TargetType_FieldName = "TargetType";
		public const string Metrics_ConversionX_FieldName = "Conversion{0}";
		public const string Metrics_AveragePosition_FieldName = "AveragePosition";
		public const string Metrics_Clicks_FieldName = "Clicks";
		public const string Metrics_Impressions_FieldName = "Impressions";
		public const string Metrics_Cost_FieldName = "Cost";
		public const string Metrics_Currency_FieldName = "Currency";
		private const string Metrics_TargetPeriodStart_FieldName = "TargetPeriodStart";
		private const string Metrics_TargetPeriodEnd_FieldName = "TargetPeriodEnd";
		
		public const string ads_Campaign_Status_FieldName = "Campaign_Status";
		public const string ads_Campaign_OriginalID_FieldName = "Campaign_OriginalID";
		public const string ads_Campaign_Name_FieldName = "Campaign_Name";
		public const string ads_Campaign_Channel_FieldName = "Campaign_Channel";
		public const string ads_Campaign_Account_FieldName = "Campaign_Account";
		public const string ads_DestinationUrl_FieldName = "DestinationUrl";
		public const string ads_OriginalID_FieldName = "OriginalID";
		public const string ads_Name_FieldName = "Name";
		public const string adUsidFieldName = "AdUsid";
		public Func<Ad, string> OnAdIdentityRequired = null;
		#endregion

		public AdDataImportSession(Delivery delivery)
			: base(delivery)
		{
			_bufferSize = int.Parse(AppSettings.Get(this, "BufferSize"));
		}

		public override void Begin(bool reset = true)
		{
			_baseTableName = string.Format("D{0}_{1}_{2}_", Delivery.Parameters["AccountID"], DateTime.Today.ToString("yyyMMdd"), Delivery._guid.ToString("N").ToLower());
			initalizeDataTablesAndBulks(reset);
		}

		private void initalizeDataTablesAndBulks(bool reset)
		{
			//initalize connection
			_sqlConnection = new SqlConnection(AppSettings.GetConnectionString(this, "DeliveriesDb"));
			_sqlConnection.Open();

			//create/truncate/do nothing  to tables
			using (SqlCommand sqlCommand = DataManager.CreateCommand("SP_BeginAdDataImportSession(@baseTableName:NvarChar,@reset:bit)", CommandType.StoredProcedure))
			{
				sqlCommand.Connection = _sqlConnection;
				sqlCommand.Parameters["@baseTableName"].Value = _baseTableName;
				sqlCommand.Parameters["@reset"].Value = reset;
				sqlCommand.ExecuteNonQuery();

			}

			#region Metrics
			_bulkMetrics = new SqlBulkCopy(_sqlConnection);
			_bulkMetrics.DestinationTableName = _baseTableName + "Metrics";
			_metricsDataTable = new DataTable(_bulkMetrics.DestinationTableName);
			_metricsDataTable.Columns.Add(adUsidFieldName);
			_metricsDataTable.Columns.Add(Metrics_TargetPeriodStart_FieldName);
			_metricsDataTable.Columns.Add(Metrics_TargetPeriodEnd_FieldName);
			_metricsDataTable.Columns.Add(Metrics_Currency_FieldName);
			_metricsDataTable.Columns.Add(Metrics_Cost_FieldName);
			_metricsDataTable.Columns.Add(Metrics_Impressions_FieldName);
			_metricsDataTable.Columns.Add(Metrics_Clicks_FieldName);
			_metricsDataTable.Columns.Add(Metrics_AveragePosition_FieldName);
			_metricsDataTable.Columns.Add("Conversion1");
			_metricsDataTable.Columns.Add("Conversion2");
			_metricsDataTable.Columns.Add("Conversion3");
			_metricsDataTable.Columns.Add("Conversion4");
			_metricsDataTable.Columns.Add("Conversion5");
			_metricsDataTable.Columns.Add("Conversion6");
			_metricsDataTable.Columns.Add("Conversion7");
			_metricsDataTable.Columns.Add("Conversion8");
			_metricsDataTable.Columns.Add("Conversion9");
			_metricsDataTable.Columns.Add("Conversion10");
			_metricsDataTable.Columns.Add("Conversion11");
			_metricsDataTable.Columns.Add("Conversion12");
			_metricsDataTable.Columns.Add("Conversion13");
			_metricsDataTable.Columns.Add("Conversion14");
			_metricsDataTable.Columns.Add("Conversion15");
			_metricsDataTable.Columns.Add("Conversion16");
			_metricsDataTable.Columns.Add("Conversion17");
			_metricsDataTable.Columns.Add("Conversion18");
			_metricsDataTable.Columns.Add("Conversion19");
			_metricsDataTable.Columns.Add("Conversion20");
			_metricsDataTable.Columns.Add("Conversion21");
			_metricsDataTable.Columns.Add("Conversion22");
			_metricsDataTable.Columns.Add("Conversion23");
			_metricsDataTable.Columns.Add("Conversion24");
			_metricsDataTable.Columns.Add("Conversion25");
			_metricsDataTable.Columns.Add("Conversion26");
			_metricsDataTable.Columns.Add("Conversion27");
			_metricsDataTable.Columns.Add("Conversion28");
			_metricsDataTable.Columns.Add("Conversion29");
			_metricsDataTable.Columns.Add("Conversion30");
			_metricsDataTable.Columns.Add("Conversion31");
			_metricsDataTable.Columns.Add("Conversion32");
			_metricsDataTable.Columns.Add("Conversion33");
			_metricsDataTable.Columns.Add("Conversion34");
			_metricsDataTable.Columns.Add("Conversion35");
			_metricsDataTable.Columns.Add("Conversion36");
			_metricsDataTable.Columns.Add("Conversion37");
			_metricsDataTable.Columns.Add("Conversion38");
			_metricsDataTable.Columns.Add("Conversion39");
			_metricsDataTable.Columns.Add("Conversion40");
			
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adUsidFieldName, adUsidFieldName));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping(Metrics_TargetPeriodStart_FieldName, Metrics_TargetPeriodStart_FieldName));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping(Metrics_TargetPeriodEnd_FieldName, Metrics_TargetPeriodEnd_FieldName));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping(Metrics_Currency_FieldName, Metrics_Currency_FieldName));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping(Metrics_Cost_FieldName, Metrics_Cost_FieldName));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping(Metrics_Impressions_FieldName, Metrics_Impressions_FieldName));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping(Metrics_Clicks_FieldName, Metrics_Clicks_FieldName));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping(Metrics_AveragePosition_FieldName, Metrics_AveragePosition_FieldName));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion1", "Conversion1"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion2", "Conversion2"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion3", "Conversion3"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion4", "Conversion4"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion5", "Conversion5"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion6", "Conversion6"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion7", "Conversion7"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion8", "Conversion8"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion9", "Conversion9"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion10", "Conversion10"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion11", "Conversion11"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion12", "Conversion12"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion13", "Conversion13"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion14", "Conversion14"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion15", "Conversion15"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion16", "Conversion16"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion17", "Conversion18"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion19", "Conversion19"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion20", "Conversion20"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion21", "Conversion21"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion22", "Conversion22"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion23", "Conversion23"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion24", "Conversion24"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion25", "Conversion25"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion26", "Conversion26"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion27", "Conversion27"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion28", "Conversion28"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion29", "Conversion29"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion30", "Conversion30"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion31", "Conversion31"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion32", "Conversion32"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion33", "Conversion33"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion34", "Conversion34"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion35", "Conversion35"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion36", "Conversion36"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion37", "Conversion37"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion38", "Conversion38"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion39", "Conversion39"));
			_bulkMetrics.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion40", "Conversion40"));
			#endregion
			#region MetricsTargetMatch

			_bulkMetricsTargetMatch = new SqlBulkCopy(_sqlConnection);
			_bulkMetricsTargetMatch.DestinationTableName = _baseTableName + "MetricsTargetMatch";
			_metricsTargetMatchDataTable = new DataTable(_bulkMetricsTargetMatch.DestinationTableName);
			_metricsTargetMatchDataTable.Columns.Add(adUsidFieldName);
			_metricsTargetMatchDataTable.Columns.Add(ads_OriginalID_FieldName);
			_metricsTargetMatchDataTable.Columns.Add(ads_DestinationUrl_FieldName);
			_metricsTargetMatchDataTable.Columns.Add(ads_TargetType_FieldName);
			_metricsTargetMatchDataTable.Columns.Add("Field1");
			_metricsTargetMatchDataTable.Columns.Add("Field2");
			_metricsTargetMatchDataTable.Columns.Add("Field3");
			_metricsTargetMatchDataTable.Columns.Add("Field4");

			_bulkMetricsTargetMatch.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adUsidFieldName, adUsidFieldName));
			_bulkMetricsTargetMatch.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_OriginalID_FieldName, ads_OriginalID_FieldName));
			_bulkMetricsTargetMatch.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_DestinationUrl_FieldName, ads_DestinationUrl_FieldName));
			_bulkMetricsTargetMatch.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field1", "Field1"));
			_bulkMetricsTargetMatch.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field2", "Field2"));
			_bulkMetricsTargetMatch.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field3", "Field3"));
			_bulkMetricsTargetMatch.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field4", "Field4"));
			_bulkMetricsTargetMatch.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_TargetType_FieldName, ads_TargetType_FieldName));



			#endregion
			#region AdCreatives

			_bulkAdCreatives = new SqlBulkCopy(_sqlConnection);
			_bulkAdCreatives.DestinationTableName = _baseTableName + "AdCreative";
			_adCreativesDataTable = new DataTable(_bulkAdCreatives.DestinationTableName);
			_adCreativesDataTable.Columns.Add(adUsidFieldName);
			_adCreativesDataTable.Columns.Add(ads_OriginalID_FieldName);
			_adCreativesDataTable.Columns.Add(ads_Name_FieldName);
			_adCreativesDataTable.Columns.Add(ads_CreativeType_FieldName);
			_adCreativesDataTable.Columns.Add("Field1");
			_adCreativesDataTable.Columns.Add("Field2");
			_adCreativesDataTable.Columns.Add("Field3");
			_adCreativesDataTable.Columns.Add("Field4");
			_adCreativesDataTable.Columns.Add("Segment1");
			_adCreativesDataTable.Columns.Add("Segment2");
			_adCreativesDataTable.Columns.Add("Segment3");
			_adCreativesDataTable.Columns.Add("Segment4");
			_adCreativesDataTable.Columns.Add("Segment5");


			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adUsidFieldName, adUsidFieldName));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_OriginalID_FieldName, ads_OriginalID_FieldName));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Name_FieldName, ads_Name_FieldName));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_CreativeType_FieldName, ads_CreativeType_FieldName));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Segment1", "Segment1"));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Segment2", "Segment2"));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Segment3", "Segment3"));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Segment4", "Segment4"));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Segment5", "Segment5"));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field1", "Field1"));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field2", "Field2"));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field3", "Field3"));
			_bulkAdCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field4", "Field4"));
			#endregion
			#region Ads

			_bulkAd = new SqlBulkCopy(_sqlConnection);
			_bulkAd.DestinationTableName = _baseTableName + "Ad";
			_adDataTable = new DataTable(_bulkAd.DestinationTableName);
			_adDataTable.Columns.Add(adUsidFieldName);
			_adDataTable.Columns.Add(ads_Name_FieldName);
			_adDataTable.Columns.Add(ads_OriginalID_FieldName);
			_adDataTable.Columns.Add(ads_DestinationUrl_FieldName);
			_adDataTable.Columns.Add(ads_Campaign_Account_FieldName);
			_adDataTable.Columns.Add(ads_Campaign_Channel_FieldName);
			_adDataTable.Columns.Add(ads_Campaign_Name_FieldName);
			_adDataTable.Columns.Add(ads_Campaign_OriginalID_FieldName);
			_adDataTable.Columns.Add(ads_Campaign_Status_FieldName);

			_bulkAd.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adUsidFieldName, adUsidFieldName));
			_bulkAd.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Name_FieldName, ads_Name_FieldName));
			_bulkAd.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_OriginalID_FieldName, ads_OriginalID_FieldName));
			_bulkAd.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_DestinationUrl_FieldName, ads_DestinationUrl_FieldName));
			_bulkAd.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Campaign_Account_FieldName, ads_Campaign_Account_FieldName));
			_bulkAd.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Campaign_Channel_FieldName, ads_Campaign_Channel_FieldName));
			_bulkAd.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Campaign_Name_FieldName, ads_Campaign_Name_FieldName));
			_bulkAd.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Campaign_OriginalID_FieldName, ads_Campaign_OriginalID_FieldName));
			_bulkAd.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Campaign_Status_FieldName, ads_Campaign_Status_FieldName));
			#endregion
			#region AdTarget
			_bulkAdTarget = new SqlBulkCopy(_sqlConnection);
			_bulkAdTarget.DestinationTableName = _baseTableName + "AdTarget";
			_adTargetDataTable = new DataTable(_bulkAdTarget.DestinationTableName);
			_adTargetDataTable.Columns.Add(adUsidFieldName);
			_adTargetDataTable.Columns.Add(ads_OriginalID_FieldName);
			_adTargetDataTable.Columns.Add(ads_DestinationUrl_FieldName);
			_adTargetDataTable.Columns.Add(ads_TargetType_FieldName);
			_adTargetDataTable.Columns.Add("Field1");
			_adTargetDataTable.Columns.Add("Field2");
			_adTargetDataTable.Columns.Add("Field3");
			_adTargetDataTable.Columns.Add("Field4");

			_bulkAdTarget.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adUsidFieldName, adUsidFieldName));
			_bulkAdTarget.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_OriginalID_FieldName, ads_OriginalID_FieldName));
			_bulkAdTarget.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_DestinationUrl_FieldName, ads_DestinationUrl_FieldName));
			_bulkAdTarget.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field1", "Field1"));
			_bulkAdTarget.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field2", "Field2"));
			_bulkAdTarget.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field3", "Field3"));
			_bulkAdTarget.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field4", "Field4"));
			_bulkAdTarget.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_TargetType_FieldName, ads_TargetType_FieldName));

			#endregion

			/*CREATE TABLE dbo.Table_1
	(
	AdUsid int NOT NULL,
	SegmentID int NOT NULL,
	Value nvarchar(300) NOT NULL,
	ValueOriginalID nvarchar(50) NULL
	)  ON [PRIMARY]
*/

		}
		private object CheckNull(object myObject)
		{
			object returnObject;
			if (myObject == null)
				returnObject = DBNull.Value;
			else
				returnObject = myObject;
			return returnObject;
		}

		private string GetAdIdentity(Ad ad)
		{
			string val;
			if (this.OnAdIdentityRequired != null)
				val = this.OnAdIdentityRequired(ad);
			else if (String.IsNullOrEmpty(ad.OriginalID))
				throw new Exception("Ad.OriginalID is required. If it is not available, provide a function for AdDataImportSession.OnAdIdentityRequired that returns a unique value for this ad.");
			else
				val = ad.OriginalID;

			return val;
		}

		public void ImportMetrics(AdMetricsUnit metrics)
		{

			string adUsid = string.Empty;
			if (metrics.Ad != null)
				adUsid = GetAdIdentity(metrics.Ad);
			DataRow row = _metricsDataTable.NewRow();



			row[adUsidFieldName] =CheckNull( adUsid);
			row[Metrics_TargetPeriodStart_FieldName] = CheckNull(this.Delivery.TargetPeriod.Start.ExactDateTime);
			row[Metrics_TargetPeriodEnd_FieldName] = CheckNull(this.Delivery.TargetPeriod.End.ExactDateTime);
			row[Metrics_Currency_FieldName] = CheckNull(metrics.Currency.Code);
			row[Metrics_Cost_FieldName] = CheckNull(metrics.Cost);
			row[Metrics_Impressions_FieldName] = CheckNull( metrics.Impressions);
			row[Metrics_Clicks_FieldName] =  CheckNull(metrics.Clicks);
			row[Metrics_AveragePosition_FieldName] = CheckNull( metrics.AveragePosition);

			//Conversions
			foreach (KeyValuePair<int, double> Conversion in metrics.Conversions)
			{
				row[string.Format(Metrics_ConversionX_FieldName, Conversion.Key)] = CheckNull( Conversion.Value);
			}

			_metricsDataTable.Rows.Add(row);
			if (_metricsDataTable.Rows.Count == _bufferSize)
			{
				_bulkMetrics.WriteToServer(_metricsDataTable);
				_metricsDataTable.Rows.Clear();
			}

			//tagetmatches
			foreach (Target target in metrics.TargetMatches)
			{
				row = _metricsTargetMatchDataTable.NewRow();
				row[adUsidFieldName] =  CheckNull(adUsid);
				row[ads_OriginalID_FieldName] = CheckNull( target.OriginalID);
				row[ads_DestinationUrl_FieldName] =  CheckNull(target.DestinationUrl);
				int targetType = GetTargetType(target.GetType());
				row[ads_TargetType_FieldName] =  CheckNull(targetType);
				foreach (FieldInfo field in target.GetType().GetFields())
				{
					if (Attribute.IsDefined(field, typeof(TargetFieldIndexAttribute)))
					{
						TargetFieldIndexAttribute TargetColumn = (TargetFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(TargetFieldIndexAttribute));
						row[string.Format(FieldX_FiledName, TargetColumn.TargetColumnIndex)] =  CheckNull(field.GetValue(target));
					}


				}
				_metricsTargetMatchDataTable.Rows.Add(row);
				if (_metricsTargetMatchDataTable.Rows.Count == _bufferSize)
				{
					_bulkMetricsTargetMatch.WriteToServer(_metricsTargetMatchDataTable);
					_metricsTargetMatchDataTable.Clear();

				}
			}

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

		public void ImportAd(Ad ad)
		{
			string adUsid = GetAdIdentity(ad);
			DataRow row = _adDataTable.NewRow();
			row[adUsidFieldName] = CheckNull( adUsid);
			row[ads_Name_FieldName] = CheckNull( ad.Name);
			row[ads_OriginalID_FieldName] =  CheckNull(ad.OriginalID);
			row[ads_DestinationUrl_FieldName] =  CheckNull(ad.DestinationUrl);
			row[ads_Campaign_Account_FieldName] =  CheckNull(ad.Campaign.Account.ID);
			row[ads_Campaign_Channel_FieldName] =  CheckNull(ad.Campaign.Channel.ID);
			row[ads_Campaign_Name_FieldName] =  CheckNull(ad.Campaign.Name);
			row[ads_Campaign_OriginalID_FieldName] = CheckNull( ad.Campaign.OriginalID);
			row[ads_Campaign_Status_FieldName] = CheckNull( ((int)ad.Campaign.Status).ToString());

			//TODO: segments
			//foreach (KeyValuePair<Segment, object> Segment in ad.Segments)
			//{
			//    row[string.Format("Segment{0}", ad.Segments)] = Conversion.Value;
			//}
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
				row[adUsidFieldName] = adUsid;
				row[ads_OriginalID_FieldName] =CheckNull( target.OriginalID);
				row[ads_DestinationUrl_FieldName] = CheckNull(target.DestinationUrl);
				int targetType = GetTargetType(target.GetType());
				row[ads_TargetType_FieldName] =CheckNull( targetType);
				foreach (FieldInfo field in target.GetType().GetFields())//TODO: GET FILEDS ONLY ONE TIME
				{
					if (Attribute.IsDefined(field, typeof(TargetFieldIndexAttribute)))
					{
						TargetFieldIndexAttribute TargetColumn = (TargetFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(TargetFieldIndexAttribute));
						row[string.Format(FieldX_FiledName, TargetColumn.TargetColumnIndex)] =CheckNull( field.GetValue(target));
					}


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
				row[adUsidFieldName] =CheckNull( adUsid);
				row[ads_OriginalID_FieldName] =CheckNull( creative.OriginalID);
				row[ads_Name_FieldName] =CheckNull( creative.Name);
				int creativeType = GetCreativeType(creative.GetType());
				row[ads_CreativeType_FieldName] = CheckNull(creativeType);
				foreach (FieldInfo field in creative.GetType().GetFields())
				{
					if (Attribute.IsDefined(field, typeof(CreativeFieldIndexAttribute)))
					{
						CreativeFieldIndexAttribute creativeColumn = (CreativeFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(CreativeFieldIndexAttribute));
						row[string.Format(FieldX_FiledName, creativeColumn.CreativeFieldIndex)] = CheckNull(field.GetValue(creative)); 
					}
				}
				_adCreativesDataTable.Rows.Add(row);
				if (_adCreativesDataTable.Rows.Count == _bufferSize)
				{
					_bulkAdCreatives.WriteToServer(_adCreativesDataTable);
					_adCreativesDataTable.Clear();
				}
			}
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
			// TODO: Fwd-only activate stored procedue

		}

		public void Dispose()
		{
			// TODO: clean up temp shit
			//TODO: add uninserted rows
			_bulkMetrics.WriteToServer(_metricsDataTable);
			_bulkAd.WriteToServer(_adDataTable);
			_bulkAdCreatives.WriteToServer(_adCreativesDataTable);
			_bulkMetricsTargetMatch.WriteToServer(_metricsTargetMatchDataTable);
			_bulkAd.WriteToServer(_adDataTable);
			_bulkAdTarget.WriteToServer(_adTargetDataTable);
			_sqlConnection.Dispose();


		}
	}
}
