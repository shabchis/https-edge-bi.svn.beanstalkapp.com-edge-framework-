using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{

	[TableInfo(Name = "Channel")]
	public partial class Channel
	{
		public int ID;
		public string Name;
		public ChannelType ChannelType;
	}

	public enum ChannelType
	{
		BackOfficeChannel,
		MarketingChannel
	}

}
