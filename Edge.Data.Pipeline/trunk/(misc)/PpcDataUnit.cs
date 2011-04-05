using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;


namespace Edge.Data.Pipeline
{
    public class AdDataUnit
    {
		public int AccountID;

		public GKey TrackerGK;
		public GKey KeywordGK;
		public GKey CreativeGK;
		public GKey CampaignGK;
		public GKey AdgroupGK;
		public GKey AdgroupKeywordGK;
		public GKey AdgroupCreativeGK;

		public DateTime TargetDateTime;
		
		public int CurrencyID;
		public double CurrencyRate;

		public long Impressions;
		public long Clicks;
		public double Cost;
		public double AveragePosition;
		public int Conversions;

		public AdDataUnitExtra Extra;

        public void Save()
        {
        }
    }

	public class AdDataUnitExtra
	{
		public string Account_OriginalID;
		public string Campaign_Name;
		public string Campaign_OriginalID;
		public string Campaign_OriginalStatus;
		public string Adgroup_Name;
		public string Adgroup_OriginalID;
		public string Keyword_Text;
		public string Keyword_OriginalID;
		public string Creative_Title;
		public string Creative_Desc1;
		public string Creative_Desc2;
		public string Creative_OriginalID;
		public string AdgroupKeyword_DestUrl;
		public string AdgroupKeyword_OriginalMatchType;
		public string AdgroupCreative_VisUrl;
		public string AdgroupCreative_DestUrl;
		public string Tracker_Value;
		public string Currency_Code;
	}
}


