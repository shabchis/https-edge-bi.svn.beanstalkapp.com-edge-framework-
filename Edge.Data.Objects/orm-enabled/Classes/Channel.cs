using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class Channel
	{
		public int ID;
		public string Name;
		public string DisplayName;
		public ChannelType ChannelType;
	}

	public enum ChannelType
	{
		Unknown = 0
	}

}
