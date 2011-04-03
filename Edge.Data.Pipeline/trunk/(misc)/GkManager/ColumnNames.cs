using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Data;

namespace Edge.Data.Pipeline.GkManager
{
	internal static class Keyword 
	{
		public class ColumnNames
		{
			public const string ID = "Keyword_GK";
			public const string AccountID = "Account_ID";
			public const string StringValue = "Keyword";
			public const string IsMonitored = "IsMonitored";
			public const string LastUpdated = "LastUpdated";
			public const string Segment1 = "Keyword_Segment1";
			public const string Segment2 = "Keyword_Segment2";
			public const string Segment3 = "Keyword_Segment3";
			public const string Segment4 = "Keyword_Segment4";
			public const string Segment5 = "Keyword_Segment5";
		}
	}

	internal static class Creative 
	{
		public static class ColumnNames
		{
			public const string GK = "Creative_GK";
			public const string AccountID = "Account_ID";
			public const string Title = "Creative_Title";
			public const string Desc1 = "Creative_Desc1";
			public const string Desc2 = "Creative_Desc2";
			public const string LastUpdated = "LastUpdated";
		}
	}

	internal static class Site
	{
		public static class ColumnNames
		{
			public const string GK = "Site_GK";
			public const string AccountID = "Account_ID";
			public const string Name = "Site";
		}
	}

	internal static class Tracker
	{
		public static class ColumnNames
		{
			public const string GK = "Gateway_GK";
			public const string AccountID = "Account_ID";
			public const string ChannelID = "Channel_ID";
			public const string CampaignGK = "Campaign_GK";
			public const string AdgroupGK = "Adgroup_GK";
			public const string AdunitID = "Adunit_ID";
			public const string ReferenceType = "Reference_Type";
			public const string ReferenceGK = "Reference_ID";
			public const string Identifier = "Gateway_id";
			public const string Name = "Gateway";
			public const string DestUrl = "Dest_URL";
			public const string LastUpdated = "LastUpdated";
		}
	}
	internal static class Campaign 
	{
		public static class ColumnNames
		{
			public const string GK = "Campaign_GK";
			public const string OriginalID = "campaignid";
			public const string AccountID = "Account_ID";
			public const string ChannelID = "Channel_ID";
			public const string Name = "campaign";
			public const string Status = "campStatus";
			public const string LastUpdated = "LastUpdated";
		}
	}

	internal static class Adgroup
	{
		public static class ColumnNames
		{
			public const string GK = "Adgroup_GK";
			public const string OriginalID = "adgroupID";
			public const string AccountID = "Account_ID";
			public const string ChannelID = "Channel_ID";
			public const string CampaignGK = "Campaign_GK";
			public const string Name = "adgroup";
			public const string Status = "agStatus";
			public const string LastUpdated = "LastUpdated";
		}
	}

	internal static class AdgroupKeyword
	{
		public static class ColumnNames
		{
			public const string GK = "PPC_Keyword_GK";
			public const string AccountID = "Account_ID";
			public const string ChannelID = "Channel_ID";
			public const string CampaignGK = "Campaign_GK";
			public const string AdgroupGK = "AdGroup_GK";
			public const string KeywordGK = "Keyword_GK";
			public const string GatewayGK = "Gateway_GK";
			public const string MatchType = "MatchType";
			public const string DestUrl = "kwDestUrl";
			public const string Status = "siteKwStatus";
			public const string LastUpdated = "LastUpdated";
		}
	}

	internal static class AdgroupCreative
	{
		public static class ColumnNames
		{
			public const string GK = "PPC_Creative_GK";
			public const string AccountID = "Account_ID";
			public const string ChannelID = "Channel_ID";
			public const string CampaignGK = "Campaign_GK";
			public const string AdgroupGK = "AdGroup_GK";
			public const string CreativeGK = "Creative_GK";
			public const string GatewayGK = "Gateway_GK";
			public const string DestUrl = "creativeDestUrl";
			public const string VisibleUrl = "creativeVisUrl";
			public const string Status = "creativeStatus";
			public const string LastUpdated = "LastUpdated";
		}
	}

	internal static class AdgroupSite
	{
		public static class ColumnNames
		{
			public const string GK = "PPC_Site_GK";
			public const string AccountID = "Account_ID";
			public const string ChannelID = "Channel_ID";
			public const string CampaignGK = "Campaign_GK";
			public const string AdgroupGK = "AdGroup_GK";
			public const string SiteGK = "Site_GK";
			public const string GatewayGK = "Gateway_GK";
			public const string DestUrl = "kwDestUrl";
			public const string MatchType = "MatchType";
		}
	}

}
