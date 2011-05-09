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
		

		private SqlBulkCopy _bulkAdMetricsUnit;
		private DataTable _adMetricsUnitDataTable;
		private SqlBulkCopy _bulkTargetMatches;
		private DataTable _targetMatchesDataTable;
		private SqlBulkCopy _bulkCreatives;
		private DataTable _creativesDataTable;
		private SqlBulkCopy _bulkAds;
		private DataTable _adsDataTable;		
		private SqlConnection _sqlConnection;
		private string _baseTableName;
		private int _bufferSize;
		private Dictionary<Type, int> _targetTypes;
		private Dictionary<Type, int> _creativeType;
		#region consts fields
		public const string ads_CreativeType_FieldName = "CreativeType";
		public const string FieldX_FiledName = "Field{0}";
		public const string ads_TargetType_FieldName = "TargetType";
		public const string adMetricsUnit_ConversionX_FieldName = "Conversion{0}";
		public const string adMetricsUnit_AveragePosition_FieldName = "AveragePosition";
		public const string adMetricsUnit_Clicks_FieldName = "Clicks";
		public const string adMetricsUnit_Impressions_FieldName = "Impressions";
		public const string adMetricsUnit_Cost_FieldName = "Cost";
		public const string adMetricsUnit_Currency_FieldName = "Currency";
		public const string adMetricsUnit_TimeStamp_FieldName = "TimeStamp";
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
			_baseTableName = string.Format("D_{0}_{1}_{2}_", DateTime.Today.ToString("yyyMMdd"), Delivery.Parameters["AccountID"],1 /*Delivery.DeliveryID*/);
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
		
			#region adMetrics
			_bulkAdMetricsUnit = new SqlBulkCopy(_sqlConnection);
			_bulkAdMetricsUnit.DestinationTableName = _baseTableName + "adMetricsUnit";
			_adMetricsUnitDataTable = new DataTable(_bulkAdMetricsUnit.DestinationTableName);
			_adMetricsUnitDataTable.Columns.Add(adUsidFieldName);
			_adMetricsUnitDataTable.Columns.Add(adMetricsUnit_TimeStamp_FieldName);
			_adMetricsUnitDataTable.Columns.Add(adMetricsUnit_Currency_FieldName);
			_adMetricsUnitDataTable.Columns.Add(adMetricsUnit_Cost_FieldName);
			_adMetricsUnitDataTable.Columns.Add(adMetricsUnit_Impressions_FieldName);
			_adMetricsUnitDataTable.Columns.Add(adMetricsUnit_Clicks_FieldName);
			_adMetricsUnitDataTable.Columns.Add(adMetricsUnit_AveragePosition_FieldName);
			_adMetricsUnitDataTable.Columns.Add("Conversion1");
			_adMetricsUnitDataTable.Columns.Add("Conversion2");
			_adMetricsUnitDataTable.Columns.Add("Conversion3");
			_adMetricsUnitDataTable.Columns.Add("Conversion4");
			_adMetricsUnitDataTable.Columns.Add("Conversion5");
			_adMetricsUnitDataTable.Columns.Add("Conversion6");
			_adMetricsUnitDataTable.Columns.Add("Conversion7");
			_adMetricsUnitDataTable.Columns.Add("Conversion8");
			_adMetricsUnitDataTable.Columns.Add("Conversion9");
			_adMetricsUnitDataTable.Columns.Add("Conversion10");
			_adMetricsUnitDataTable.Columns.Add("Conversion11");
			_adMetricsUnitDataTable.Columns.Add("Conversion12");
			_adMetricsUnitDataTable.Columns.Add("Conversion13");
			_adMetricsUnitDataTable.Columns.Add("Conversion14");
			_adMetricsUnitDataTable.Columns.Add("Conversion15");
			_adMetricsUnitDataTable.Columns.Add("Conversion16");
			_adMetricsUnitDataTable.Columns.Add("Conversion17");
			_adMetricsUnitDataTable.Columns.Add("Conversion18");
			_adMetricsUnitDataTable.Columns.Add("Conversion19");
			_adMetricsUnitDataTable.Columns.Add("Conversion20");
			_adMetricsUnitDataTable.Columns.Add("Conversion21");
			_adMetricsUnitDataTable.Columns.Add("Conversion22");
			_adMetricsUnitDataTable.Columns.Add("Conversion23");
			_adMetricsUnitDataTable.Columns.Add("Conversion24");
			_adMetricsUnitDataTable.Columns.Add("Conversion25");
			_adMetricsUnitDataTable.Columns.Add("Conversion26");
			_adMetricsUnitDataTable.Columns.Add("Conversion27");
			_adMetricsUnitDataTable.Columns.Add("Conversion28");
			_adMetricsUnitDataTable.Columns.Add("Conversion29");
			_adMetricsUnitDataTable.Columns.Add("Conversion30");
			_adMetricsUnitDataTable.Columns.Add("Conversion31");
			_adMetricsUnitDataTable.Columns.Add("Conversion32");
			_adMetricsUnitDataTable.Columns.Add("Conversion33");
			_adMetricsUnitDataTable.Columns.Add("Conversion34");
			_adMetricsUnitDataTable.Columns.Add("Conversion35");
			_adMetricsUnitDataTable.Columns.Add("Conversion36");
			_adMetricsUnitDataTable.Columns.Add("Conversion37");
			_adMetricsUnitDataTable.Columns.Add("Conversion38");
			_adMetricsUnitDataTable.Columns.Add("Conversion39");
			_adMetricsUnitDataTable.Columns.Add("Conversion40");

			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adUsidFieldName, adUsidFieldName));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adMetricsUnit_TimeStamp_FieldName, adMetricsUnit_TimeStamp_FieldName));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adMetricsUnit_Currency_FieldName, adMetricsUnit_Currency_FieldName));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adMetricsUnit_Cost_FieldName, adMetricsUnit_Cost_FieldName));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adMetricsUnit_Impressions_FieldName, adMetricsUnit_Impressions_FieldName));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adMetricsUnit_Clicks_FieldName, adMetricsUnit_Clicks_FieldName));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adMetricsUnit_AveragePosition_FieldName, adMetricsUnit_AveragePosition_FieldName));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion1", "Conversion1"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion2", "Conversion2"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion3", "Conversion3"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion4", "Conversion4"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion5", "Conversion5"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion6", "Conversion6"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion7", "Conversion7"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion8", "Conversion8"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion9", "Conversion9"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion10", "Conversion10"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion11", "Conversion11"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion12", "Conversion12"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion13", "Conversion13"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion14", "Conversion14"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion15", "Conversion15"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion16", "Conversion16"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion17", "Conversion18"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion19", "Conversion19"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion20", "Conversion20"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion21", "Conversion21"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion22", "Conversion22"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion23", "Conversion23"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion24", "Conversion24"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion25", "Conversion25"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion26", "Conversion26"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion27", "Conversion27"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion28", "Conversion28"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion29", "Conversion29"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion30", "Conversion30"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion31", "Conversion31"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion32", "Conversion32"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion33", "Conversion33"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion34", "Conversion34"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion35", "Conversion35"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion36", "Conversion36"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion37", "Conversion37"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion38", "Conversion38"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion39", "Conversion39"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Conversion40", "Conversion40"));
			#endregion
			#region TargetMatches
			
			_bulkTargetMatches = new SqlBulkCopy(_sqlConnection);
			_bulkTargetMatches.DestinationTableName = _baseTableName + "targetMatches";
			_targetMatchesDataTable = new DataTable(_bulkTargetMatches.DestinationTableName);
			_targetMatchesDataTable.Columns.Add(adUsidFieldName);
			_targetMatchesDataTable.Columns.Add(ads_OriginalID_FieldName);
			_targetMatchesDataTable.Columns.Add(ads_DestinationUrl_FieldName);
			_targetMatchesDataTable.Columns.Add(ads_TargetType_FieldName);
			_targetMatchesDataTable.Columns.Add("Field1");
			_targetMatchesDataTable.Columns.Add("Field2");
			_targetMatchesDataTable.Columns.Add("Field3");
			_targetMatchesDataTable.Columns.Add("Field4");

			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adUsidFieldName, adUsidFieldName));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_OriginalID_FieldName, ads_OriginalID_FieldName));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_DestinationUrl_FieldName, ads_DestinationUrl_FieldName));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field1", "Field1"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field2", "Field2"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field3", "Field3"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field4", "Field4"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_TargetType_FieldName, ads_TargetType_FieldName));



			#endregion
			#region Creatives
			
			_bulkCreatives = new SqlBulkCopy(_sqlConnection);
			_bulkCreatives.DestinationTableName = _baseTableName + "creatives";
			_creativesDataTable = new DataTable(_bulkCreatives.DestinationTableName);
			_creativesDataTable.Columns.Add(adUsidFieldName);
			_creativesDataTable.Columns.Add(ads_OriginalID_FieldName);
			_creativesDataTable.Columns.Add(ads_Name_FieldName);
			_creativesDataTable.Columns.Add(ads_CreativeType_FieldName);
			_creativesDataTable.Columns.Add("Field1");
			_creativesDataTable.Columns.Add("Field2");
			_creativesDataTable.Columns.Add("Field3");
			_creativesDataTable.Columns.Add("Field4");

			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adUsidFieldName, adUsidFieldName));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_OriginalID_FieldName, ads_OriginalID_FieldName));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Name_FieldName, ads_Name_FieldName));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_CreativeType_FieldName, ads_CreativeType_FieldName));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field1", "Field1"));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field2", "Field2"));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field3", "Field3"));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field4", "Field4"));
			#endregion
			#region Ads
		
			_bulkAds = new SqlBulkCopy(_sqlConnection);
			_bulkAds.DestinationTableName = _baseTableName + "ads";
			_adsDataTable = new DataTable(_bulkAds.DestinationTableName);
			_adsDataTable.Columns.Add(adUsidFieldName);
			_adsDataTable.Columns.Add(ads_Name_FieldName);
			_adsDataTable.Columns.Add(ads_OriginalID_FieldName);
			_adsDataTable.Columns.Add(ads_DestinationUrl_FieldName);
			_adsDataTable.Columns.Add(ads_Campaign_Account_FieldName);
			_adsDataTable.Columns.Add(ads_Campaign_Channel_FieldName);
			_adsDataTable.Columns.Add(ads_Campaign_Name_FieldName);
			_adsDataTable.Columns.Add(ads_Campaign_OriginalID_FieldName);
			_adsDataTable.Columns.Add(ads_Campaign_Status_FieldName);

			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping(adUsidFieldName, adUsidFieldName));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Name_FieldName, ads_Name_FieldName));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_OriginalID_FieldName, ads_OriginalID_FieldName));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_DestinationUrl_FieldName, ads_DestinationUrl_FieldName));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Campaign_Account_FieldName, ads_Campaign_Account_FieldName));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Campaign_Channel_FieldName, ads_Campaign_Channel_FieldName));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Campaign_Name_FieldName, ads_Campaign_Name_FieldName));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Campaign_OriginalID_FieldName, ads_Campaign_OriginalID_FieldName));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping(ads_Campaign_Status_FieldName, ads_Campaign_Status_FieldName));		
			#endregion

			//todo:split targetmacthces from targets/

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
			string adUsid = GetAdIdentity(metrics.Ad);
			DataRow row = _adMetricsUnitDataTable.NewRow();

			

			row[adUsidFieldName] = adUsid;
			row[adMetricsUnit_TimeStamp_FieldName] = metrics.TimeStamp;
			row[adMetricsUnit_Currency_FieldName] = metrics.Currency.Code;
			row[adMetricsUnit_Cost_FieldName] = metrics.Cost;
			row[adMetricsUnit_Impressions_FieldName] = metrics.Impressions;
			row[adMetricsUnit_Clicks_FieldName] = metrics.Clicks;
			row[adMetricsUnit_AveragePosition_FieldName] = metrics.AveragePosition;

			//Conversions
			foreach (KeyValuePair<int, double> Conversion in metrics.Conversions)
			{
				row[string.Format(adMetricsUnit_ConversionX_FieldName, Conversion.Key)] = Conversion.Value;
			}

			_adMetricsUnitDataTable.Rows.Add(row);
			if (_adMetricsUnitDataTable.Rows.Count == _bufferSize)
			{
				_bulkAdMetricsUnit.WriteToServer(_adMetricsUnitDataTable);
				_adMetricsUnitDataTable.Rows.Clear();
			}

			//tagetmatches
			foreach (Target target in metrics.TargetMatches)
			{
				row = _targetMatchesDataTable.NewRow();
				row[adUsidFieldName] = adUsid;
				row[ads_OriginalID_FieldName] = target.OriginalID;
				row[ads_DestinationUrl_FieldName] = target.DestinationUrl;
				int targetType=	GetTargetType(target.GetType());
				row[ads_TargetType_FieldName] = targetType;
				foreach (FieldInfo field in target.GetType().GetFields())
				{
					if (Attribute.IsDefined(field, typeof(TargetFieldIndexAttribute)))
					{
						TargetFieldIndexAttribute TargetColumn = (TargetFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(TargetFieldIndexAttribute));
						row[string.Format(FieldX_FiledName,TargetColumn.TargetColumnIndex)] = field.GetValue(target);					
					}
					
					
				}
				_targetMatchesDataTable.Rows.Add(row);
				if (_targetMatchesDataTable.Rows.Count==_bufferSize)
				{
					_bulkTargetMatches.WriteToServer(_targetMatchesDataTable);
					_targetMatchesDataTable.Clear();
					
				}
			}
			
		}

		private int GetTargetType(Type type)
		{
			int targetType = -1;
			if (_targetTypes==null)
				_targetTypes=new Dictionary<Type,int>();
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
			DataRow row = _adsDataTable.NewRow();
			row[adUsidFieldName] = adUsid;
			row[ads_Name_FieldName] = ad.Name;
			row[ads_OriginalID_FieldName] = ad.OriginalID;
			row[ads_DestinationUrl_FieldName] = ad.DestinationUrl;
			row[ads_Campaign_Account_FieldName] = ad.Campaign.Account.ID;
			row[ads_Campaign_Channel_FieldName] = ad.Campaign.Channel.ID;
			row[ads_Campaign_Name_FieldName] = ad.Campaign.Name;
			row[ads_Campaign_OriginalID_FieldName] = ad.Campaign.OriginalID;
			row[ads_Campaign_Status_FieldName] =( (int)ad.Campaign.Status).ToString();

			//TODO: segments
			//foreach (KeyValuePair<Segment, object> Segment in ad.Segments)
			//{
			//    row[string.Format("Segment{0}", ad.Segments)] = Conversion.Value;
			//}
			_adsDataTable.Rows.Add(row);
			if (_adsDataTable.Rows.Count == _bufferSize)
			{
				_bulkAds.WriteToServer(_adsDataTable);
				_adsDataTable.Clear();
			}
			//Targets
			foreach (Target target in ad.Targets)
			{
				row = _targetMatchesDataTable.NewRow();
				row[adUsidFieldName] = adUsid;
				row[ads_OriginalID_FieldName] = target.OriginalID;
				row[ads_DestinationUrl_FieldName] = target.DestinationUrl;
				int targetType = GetTargetType(target.GetType());
				row[ads_TargetType_FieldName] = targetType;
				foreach (FieldInfo field in target.GetType().GetFields())//TODO: GET FILEDS ONLY ONE TIME
				{
					if (Attribute.IsDefined(field, typeof(TargetFieldIndexAttribute)))
					{
						TargetFieldIndexAttribute TargetColumn = (TargetFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(TargetFieldIndexAttribute));
						row[string.Format(FieldX_FiledName, TargetColumn.TargetColumnIndex)] = field.GetValue(target);
					}


				}
				_targetMatchesDataTable.Rows.Add(row);
				if (_targetMatchesDataTable.Rows.Count == _bufferSize)
				{
					_bulkTargetMatches.WriteToServer(_targetMatchesDataTable);
					_targetMatchesDataTable.Clear();
				}

			}

			//Creatives
			foreach (Creative creative in ad.Creatives)
			{
				row = _creativesDataTable.NewRow();
				row[adUsidFieldName] = adUsid;
				row[ads_OriginalID_FieldName]=creative.OriginalID;
				row[ads_Name_FieldName] = creative.Name;
				int creativeType =GetCreativeType(creative.GetType());
				row[ads_CreativeType_FieldName] = creativeType;
				foreach (FieldInfo field in creative.GetType().GetFields())
				{
					if (Attribute.IsDefined(field, typeof(CreativeFieldIndexAttribute)))
					{
						CreativeFieldIndexAttribute creativeColumn = (CreativeFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(CreativeFieldIndexAttribute));
						row[string.Format(FieldX_FiledName, creativeColumn.CreativeFieldIndex)] = field.GetValue(creative);
					}
				}
				_creativesDataTable.Rows.Add(row);
				if (_creativesDataTable.Rows.Count == _bufferSize)
				{
					_bulkCreatives.WriteToServer(_creativesDataTable);
					_creativesDataTable.Clear();
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
			_bulkAdMetricsUnit.WriteToServer(_adMetricsUnitDataTable);
			_bulkAds.WriteToServer(_adsDataTable);
			_bulkCreatives.WriteToServer(_creativesDataTable);
			_bulkTargetMatches.WriteToServer(_targetMatchesDataTable);
			_bulkAds.WriteToServer(_adsDataTable);
			_sqlConnection.Dispose();
			
			
		}
	}
}
