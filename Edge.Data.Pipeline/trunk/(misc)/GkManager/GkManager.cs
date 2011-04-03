using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Timers;
using System.Xml.Serialization;
using Edge.Core.Configuration;
using Edge.Core.Data;
using Edge.Core.Services;
using Edge.Core.Utilities;
using System.Data.SqlClient;
using System.Data;

namespace Edge.Data.Pipeline.GkManager
{
	/// <summary>
	/// 
	/// </summary>
	public static class GkManager
	{
		static Dictionary<Type, string> _commands = new Dictionary<Type, string>();

		static GkManager()
		{
			_commands[typeof(Keyword)] = String.Format(
				"GkManager_GetKeywordGK(@{0}:Int, @{1}:NVarChar)",
					Keyword.ColumnNames.AccountID,
					Keyword.ColumnNames.StringValue
				);

			_commands[typeof(Creative)] = String.Format(
				"GkManager_GetCreativeGK(@{0}:Int, @{1}:NVarChar, @{2}:NVarChar, @{3}:NVarChar)",
					Creative.ColumnNames.AccountID,
					Creative.ColumnNames.Title,
					Creative.ColumnNames.Desc1,
					Creative.ColumnNames.Desc2
				);

			_commands[typeof(Site)] = String.Format(
				"GkManager_GetSiteGK(@{0}:Int, @{1}:NVarChar)",
					Site.ColumnNames.AccountID,
					Site.ColumnNames.Name
				);

			_commands[typeof(Campaign)] = String.Format(
				"GkManager_GetCampaignGK(@{0}:Int, @{1}:Int, @{2}:NVarChar, @{3}:BigInt)",
					Campaign.ColumnNames.AccountID,
					Campaign.ColumnNames.ChannelID,
					Campaign.ColumnNames.Name,
					Campaign.ColumnNames.OriginalID
				);

			_commands[typeof(Adgroup)] = String.Format(
				"GkManager_GetAdgroupGK(@{0}:Int, @{1}:Int, @{2}:BigInt, @{3}:NVarChar, @{4}:BigInt)",
					Adgroup.ColumnNames.AccountID,
					Adgroup.ColumnNames.ChannelID,
					Adgroup.ColumnNames.CampaignGK,
					Adgroup.ColumnNames.Name,
					Adgroup.ColumnNames.OriginalID
				);

			_commands[typeof(Tracker)] = String.Format(
				"GkManager_GetGatewayGK(@{0}:Int, @{1}:BigInt, @{2}:Int, @{3}:BigInt, @{4}:BigInt, @{5}:NVarChar, @{6}:NVarChar, @{7}:Int, @{8}:BigInt)",
					Tracker.ColumnNames.AccountID,
					Tracker.ColumnNames.Identifier,
					Tracker.ColumnNames.ChannelID,
					Tracker.ColumnNames.CampaignGK,
					Tracker.ColumnNames.AdgroupGK,
					Tracker.ColumnNames.Name,
					Tracker.ColumnNames.DestUrl,
					Tracker.ColumnNames.ReferenceType,
					Tracker.ColumnNames.ReferenceGK
				);

			_commands[typeof(AdgroupKeyword)] = String.Format(
				"GkManager_GetAdgroupKeywordGK(@{0}:Int, @{1}:Int, @{2}:BigInt, @{3}:BigInt, @{4}:BigInt, @{5}:Int, @{6}:NVarChar, @{7}:BigInt)",
					AdgroupKeyword.ColumnNames.AccountID,
					AdgroupKeyword.ColumnNames.ChannelID,
					AdgroupKeyword.ColumnNames.CampaignGK,
					AdgroupKeyword.ColumnNames.AdgroupGK,
					AdgroupKeyword.ColumnNames.KeywordGK,
					AdgroupKeyword.ColumnNames.MatchType,
					AdgroupKeyword.ColumnNames.DestUrl,
					AdgroupKeyword.ColumnNames.GatewayGK
				);

			_commands[typeof(AdgroupCreative)] = String.Format(
				"GkManager_GetAdgroupCreativeGK(@{0}:Int, @{1}:Int, @{2}:BigInt, @{3}:BigInt, @{4}:BigInt, @{5}:NVarChar, @{6}:NVarChar, @{7}:BigInt)",
					AdgroupCreative.ColumnNames.AccountID,
					AdgroupCreative.ColumnNames.ChannelID,
					AdgroupCreative.ColumnNames.CampaignGK,
					AdgroupCreative.ColumnNames.AdgroupGK,
					AdgroupCreative.ColumnNames.CreativeGK,
					AdgroupCreative.ColumnNames.DestUrl,
					AdgroupCreative.ColumnNames.VisibleUrl,
					AdgroupCreative.ColumnNames.GatewayGK
				);

			_commands[typeof(AdgroupSite)] = String.Format(
				"GkManager_GetAdgroupSiteGK(@{0}:Int, @{1}:Int, @{2}:BigInt, @{3}:BigInt, @{4}:BigInt, @{5}:NVarChar, @{6}:Int, @{7}:BigInt)",
					AdgroupSite.ColumnNames.AccountID,
					AdgroupSite.ColumnNames.ChannelID,
					AdgroupSite.ColumnNames.CampaignGK,
					AdgroupSite.ColumnNames.AdgroupGK,
					AdgroupSite.ColumnNames.SiteGK,
					AdgroupSite.ColumnNames.DestUrl,
					AdgroupSite.ColumnNames.MatchType,
					AdgroupSite.ColumnNames.GatewayGK
				);
		}

		static long GetID(Type businessObjectType, params object[] parameters)
		{
			string cmdText = null;
			if (!_commands.TryGetValue(businessObjectType, out cmdText))
				throw new ArgumentException(String.Format("The specified business object {0} does not have a lookup command associated with it.", businessObjectType.Name));

			object retValue;
			using (SqlCommand cmd = DataManager.CreateCommand(cmdText, CommandType.StoredProcedure))
			{
				// Assuming correct order!
				for(int i = 0; i < cmd.Parameters.Count; i++)
				{
					cmd.Parameters[i].Value = i > parameters.Length-1 || parameters[i] == null ?
						(object)DBNull.Value  :
						parameters[i];
				}

				using (DataManager.Current.OpenConnection())
				{
					DataManager.Current.AssociateCommands(cmd);
					retValue = cmd.ExecuteScalar();
				}
			}

			if (retValue is DBNull)
				throw new ArgumentException(String.Format("{0} GK could not be retrieved because one or parameters were passed as null.", businessObjectType.Name));

			return (long) retValue;
		}

		#region Public static methods
		/*=========================*/

		public static long GetKeywordGK(int accountID, string value)
		{
			return GetID(typeof(Keyword),
				accountID,
				value
			);
		}

		public static long GetSiteGK(int accountID, string siteName)
		{
			return GetID(typeof(Site),
				accountID,
				siteName
			);
		}

		public static long GetCreativeGK(int accountID, string title, string desc1, string desc2)
		{
			return GetID(typeof(Creative),
				accountID,
				title,
				desc1,
				desc2
			);
		}

		public static long GetTrackerGK(int accountID, string identifier, int channelID, long? campaignGK, long? adgroupGK, string name, string destinationUrl, TrackerReferenceType? referenceType, long? referenceGK)
		{
			return GetID(typeof(Tracker),
				accountID,
				identifier,
				channelID,
				campaignGK,
				adgroupGK,
				name,
				destinationUrl,
				(referenceType.HasValue ? (object) (int) referenceType : null),
				referenceGK
			);
		}

		public static long GetTrackerGK(int accountID, long identifier)
		{
			return GetID(typeof(Tracker), 
				accountID,
				identifier
			);
		}

		public static long GetCampaignGK(int accountID, int channelID, string name, string originalID)
		{
			return GetID(typeof(Campaign), 
				accountID,
				channelID,
				name,
				originalID
			);
		}

		public static long GetAdgroupGK(int accountID, int channelID, long campaignGK, string name, string originalID)
		{
			return GetID(typeof(Adgroup), 
				accountID,
				channelID,
				campaignGK,
				name,
				originalID
			);
		}

		public static long GetAdgroupKeywordGK(int accountID, int channelID, long campaignGK, long adgroupGK, long keywordGK, MatchType matchType, string destinationUrl, long? gatewayGK)
		{
			return GetID(typeof(AdgroupKeyword), 
				accountID,
				channelID,
				campaignGK,
				adgroupGK,
				keywordGK,
				(int) matchType,
				destinationUrl,
				gatewayGK
			);
		}

		public static long GetAdgroupCreativeGK(int accountID, int channelID, long campaignGK, long adgroupGK, long creativeGK, string destinationUrl, string visibleUrl, long? gatewayGK)
		{
			return GetID(typeof(AdgroupCreative), 
				accountID,
				channelID,
				campaignGK,
				adgroupGK,
				creativeGK,
				destinationUrl,
				visibleUrl,
				gatewayGK
			);
		}

		public static long GetAdgroupSiteGK(int accountID, int channelID, long campaignGK, long adgroupGK, long siteGK, string destinationUrl, MatchType matchType, long? gatewayGK)
		{
			return GetID(typeof(AdgroupSite), 
				accountID,
				channelID,
				campaignGK,
				adgroupGK,
				siteGK,
				destinationUrl,
				(int) matchType,
				gatewayGK
			);
		}

		/*=========================*/
		#endregion

	}

}
	