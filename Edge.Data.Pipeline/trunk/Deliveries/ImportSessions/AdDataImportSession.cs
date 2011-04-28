using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline.Objects;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Data;
using System.Reflection;


namespace Edge.Data.Pipeline.Deliveries
{

	public class AdDataImportSession : DeliveryImportSession<AdMetricsUnit>, IDisposable
	{
		public Func<Ad, string> OnAdIdentityRequired = null;

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

		public AdDataImportSession(Delivery delivery)
			: base(delivery)
		{
			_bufferSize = int.Parse(AppSettings.Get(this, "BufferSize"));
		}

		public override void Begin(bool reset = true)
		{
			/*check if table exists if not---> create tables
 			                        if yes---> if reset true--->clear table
			 *								   if reset false-->don't touch tables ,and then???
			*/
			//TODO: check what to do if table exists and reset = false
			// TODO: setup temp table

			//Get base table name
			_baseTableName = string.Format("D_{0}_{1}_{2}_", DateTime.Today.ToString("yyyMMdd"), Delivery.Parameters["AccountID"], Delivery.DeliveryID);


			//Create SqlBulkCopy for all tables
			//AdMetricsUnit





			initalizeDataTablesAndBulks();


		}

		private void initalizeDataTablesAndBulks()
		{
			//initalize connection
			_sqlConnection = new SqlConnection(AppSettings.GetConnectionString(this, "DeliveriesDb"));
			#region adMetrics
			_bulkAdMetricsUnit = new SqlBulkCopy(_sqlConnection);
			_bulkAdMetricsUnit.DestinationTableName = _baseTableName + "AdMetricsUnit";
			_adMetricsUnitDataTable = new DataTable(_bulkAdMetricsUnit.DestinationTableName);
			_adMetricsUnitDataTable.Columns.Add("AdUsid");
			_adMetricsUnitDataTable.Columns.Add("TimeStamp");
			_adMetricsUnitDataTable.Columns.Add("Currency");
			_adMetricsUnitDataTable.Columns.Add("Cost");
			_adMetricsUnitDataTable.Columns.Add("Impressions");
			_adMetricsUnitDataTable.Columns.Add("Clicks");
			_adMetricsUnitDataTable.Columns.Add("AveragePosition");
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

			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("TimeStamp", "TimeStamp"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Currency", "Currency"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Cost", "Cost"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Impressions", "Impressions"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Clicks", "Clicks"));
			_bulkAdMetricsUnit.ColumnMappings.Add(new SqlBulkCopyColumnMapping("AveragePosition", "AveragePosition"));
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
			_bulkTargetMatches.DestinationTableName = _baseTableName + "TargetMatches";
			_targetMatchesDataTable = new DataTable(_bulkTargetMatches.DestinationTableName);
			_targetMatchesDataTable.Columns.Add("AdUsid");
			_targetMatchesDataTable.Columns.Add("OriginalID");
			_targetMatchesDataTable.Columns.Add("DestinationUrl");
			_targetMatchesDataTable.Columns.Add("TargetType");
			_targetMatchesDataTable.Columns.Add("Field1");
			_targetMatchesDataTable.Columns.Add("Field2");
			_targetMatchesDataTable.Columns.Add("Field3");
			_targetMatchesDataTable.Columns.Add("Field4");

			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("AdUsid", "AdUsid"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("OriginalID", "OriginalID"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("DestinationUrl", "DestinationUrl"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field1", "Field1"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field2", "Field2"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field3", "Field3"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field4", "Field4"));
			_bulkTargetMatches.ColumnMappings.Add(new SqlBulkCopyColumnMapping("TargetType", "TargetType"));



			#endregion
			#region Creatives
			
			_bulkCreatives = new SqlBulkCopy(_sqlConnection);
			_bulkCreatives.DestinationTableName = _baseTableName + "Creatives";
			_creativesDataTable = new DataTable(_bulkCreatives.DestinationTableName);
			_creativesDataTable.Columns.Add("AdUsid");
			_creativesDataTable.Columns.Add("OriginalID");
			_creativesDataTable.Columns.Add("Name");
			_creativesDataTable.Columns.Add("CreativeType");
			_creativesDataTable.Columns.Add("Field1");
			_creativesDataTable.Columns.Add("Field2");
			_creativesDataTable.Columns.Add("Field3");
			_creativesDataTable.Columns.Add("Field4");

			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("AdUsid", "AdUsid"));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("OriginalID", "OriginalID"));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Name", "Name"));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("CreativeType", "CreativeType"));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field1", "Field1"));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field2", "Field2"));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field3", "Field3"));
			_bulkCreatives.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Field4", "Field4"));
			#endregion
			#region Ads
			_bulkAds = new SqlBulkCopy(_sqlConnection);
			_bulkAds.DestinationTableName = _baseTableName + "Ads";
			_adsDataTable = new DataTable(_bulkAds.DestinationTableName);
			_adsDataTable.Columns.Add("AdUsid");
			_adsDataTable.Columns.Add("Name");
			_adsDataTable.Columns.Add("OriginalID");
			_adsDataTable.Columns.Add("DestinationUrl");
			_adsDataTable.Columns.Add("Campaign_Account");
			_adsDataTable.Columns.Add("Campaign_Channel");
			_adsDataTable.Columns.Add("Campaign_Name");
			_adsDataTable.Columns.Add("Campaign_OriginalID");
			_adsDataTable.Columns.Add("Campaign_Status");

			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping("AdUsid", "AdUsid"));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Name", "Name"));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping("OriginalID", "OriginalID"));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping("DestinationUrl", "DestinationUrl"));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Campaign_Account", "Campaign_Account"));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Campaign_Channel", "Campaign_Channel"));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Campaign_Name", "Campaign_Name"));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Campaign_OriginalID", "Campaign_OriginalID"));
			_bulkAds.ColumnMappings.Add(new SqlBulkCopyColumnMapping("Campaign_Status", "Campaign_Status"));		
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

			// !@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#
			//
			// TODO: all field names = const!!!
			//
			// !@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#!@$#

			row["AdUsid"] = adUsid;
			row["TimeStamp"] = metrics.TimeStamp;
			row["Currency"] = metrics.Currency;
			row["Cost"] = metrics.Cost;
			row["Impressions"] = metrics.Impressions;
			row["Clicks"] = metrics.Clicks;
			row["AveragePosition"] = metrics.AveragePosition;

			//Conversions
			foreach (KeyValuePair<int, double> Conversion in metrics.Conversions)
			{
				row[string.Format("Conversion{0}", Conversion.Key)] = Conversion.Value;
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
				row["AdUsid"] = adUsid;
				row["OriginalID"] = target.OriginalID;
				row["DestinationUrl"] = target.DestinationUrl;
				int targetType=	GetTargetType(target.GetType());
				row["TargetType"] = targetType;
				foreach (FieldInfo field in target.GetType().GetFields())
				{
					if (Attribute.IsDefined(field, typeof(TargetColumnAttribute)))
					{
						TargetColumnAttribute TargetColumn=(TargetColumnAttribute)Attribute.GetCustomAttribute(field,typeof(TargetColumnAttribute));
						row[string.Format("Field{0}",TargetColumn.TargetColumnID)] = field.GetValue(target);					
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
				if (Attribute.IsDefined(type, typeof(TargetTypeAttribute)))
				{
					targetType = ((TargetTypeAttribute)Attribute.GetCustomAttribute(type, typeof(TargetTypeAttribute))).TargetTypeID;
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
			row["AdUsid"] = adUsid;
			row["Name"] = ad.Name;
			row["OriginalID"] = ad.OriginalID;
			row["DestinationUrl"] = ad.DestinationUrl;
			row["Campaign_Account"] = ad.Campaign.Account;
			row["Campaign_Channel"] = ad.Campaign.Channel;
			row["Campaign_Name"] = ad.Campaign.Name;
			row["Campaign_OriginalID"] = ad.Campaign.OriginalID;
			row["Campaign_Status"] = ad.Campaign.Status;

			//TODO: segments
			//foreach (KeyValuePair<Segment, object> Segment in ad.Segments)
			//{
			//    row[string.Format("Segment{0}", ad.Segments)] = Conversion.Value;
			//}

			if (_adsDataTable.Rows.Count == _bufferSize)
			{
				_bulkAds.WriteToServer(_adsDataTable);
				_adsDataTable.Clear();
			}
			//Targets
			foreach (Target target in ad.Targets)
			{
				row = _targetMatchesDataTable.NewRow();
				row["AdUsid"] = adUsid;
				row["OriginalID"] = target.OriginalID;
				row["DestinationUrl"] = target.DestinationUrl;
				int targetType = GetTargetType(target.GetType());
				row["TargetType"] = targetType;
				foreach (FieldInfo field in target.GetType().GetFields())//TODO: GET FILEDS ONLY ONE TIME
				{
					if (Attribute.IsDefined(field, typeof(TargetColumnAttribute)))
					{
						TargetColumnAttribute TargetColumn = (TargetColumnAttribute)Attribute.GetCustomAttribute(field, typeof(TargetColumnAttribute));
						row[string.Format("Field{0}", TargetColumn.TargetColumnID)] = field.GetValue(target);
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
				row["AdUsid"] = adUsid;
				row["OriginalID"]=creative.OriginalID;
				row["Name"] = creative.Name;
				int creativeType = GetTargetType(creative.GetType());
				row["CreativeType"] = creativeType;
				foreach (FieldInfo field in creative.GetType().GetFields())
				{
					if (Attribute.IsDefined(field, typeof(CreativeColumnAttribute)))
					{
						CreativeColumnAttribute creativeColumn = (CreativeColumnAttribute)Attribute.GetCustomAttribute(field, typeof(CreativeColumnAttribute));
						row[string.Format("Field{0}", creativeColumn.CreativeColumnID)] = field.GetValue(creative);
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

		public override void Commit()
		{
			// TODO: Fwd-only activate stored procedue
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			// TODO: clean up temp shit
			//TODO: add uninserted rows
			_bulkAdMetricsUnit.WriteToServer(_adMetricsUnitDataTable);
			throw new NotImplementedException();
		}
	}
}
