using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;


namespace Edge.Data.Pipeline
{
    public class PpcDataUnit
    {
		public int AccountID;

		public GKey TrackerGK;
		public GKey KeywordGK;
		public GKey CreativeGK;
		public GKey SiteGK;
		public GKey CampaignGK;
		public GKey AdgroupGK;
		public GKey AdgroupKeywordGK;
		public GKey AdgroupCreativeGK;
		public GKey AdgroupSiteGK;

		public DateTime DateTime;
		
		public int CurrencyID;
		public double CurrencyRate;

		public long Impressions;
		public long Clicks;
		public double Cost;
		public double AveragePosition;
		public int Conversions;

		public PpcDataUnitExtra Extra;

        public void Save()
        {
        }

		public PpcDataUnit Merge(PpcDataUnit unit)
		{
			throw new NotImplementedException();
		}
    }

	public class PpcDataUnitExtra
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


