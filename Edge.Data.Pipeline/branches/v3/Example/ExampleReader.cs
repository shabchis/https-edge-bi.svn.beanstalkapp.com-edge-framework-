using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Services.Data.Pipeline;
using System.Data;

namespace Services.Data.Pipeline
{
	public class ExampleReader : XPathRowReader<RowT>, IDeliveryFileReader
	{
		public ExampleReader(string fileName): base(url, "/stats/ad_stats")
		{
			this.OnNextRowRequired = innerReader => new PpcExampleRow
			{
				AccountName = innerReader.GetAttribute("account"),
				CampaignName = innerReader.GetAttribute("campaign"),
				AdgroupName = innerReader.GetAttribute("adgroup"),
				Keyword = innerReader.GetAttribute("kw"),
				CreativeID = innerReader.GetAttribute("ad_id"),
				Clicks = innerReader.GetAttribute("clicks"),
				Impressions = innerReader.GetAttribute("imps"),
				Cost = innerReader.GetAttribute("total"),
			};
		}

		#region IDeliveryFileReader Members

		public DeliveryFile File
		{
			get;
			set;
		}

		#endregion
	}

	public class PpcExampleRow
	{
		public string AccountName;
		public string CampaignName;
		public string AdgroupName;
		public string Keyword;
		public int CreativeID;
		public int Clicks;
		public int Impressions;
		public float Cost;
	}
}
