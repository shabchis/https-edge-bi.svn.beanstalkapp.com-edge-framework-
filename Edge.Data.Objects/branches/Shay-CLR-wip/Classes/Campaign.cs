using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[TableInfo(Name = "Campaign")]
	public partial class Campaign : ChannelSpecificObject
	{
		public string CampaignName;
		public double Budget;
	}

}
