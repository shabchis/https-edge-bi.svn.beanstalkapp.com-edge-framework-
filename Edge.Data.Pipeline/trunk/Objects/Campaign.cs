using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class Campaign
	{
		public Account Account;
		public Channel Channel;
		public string Name;
		public string OriginalID;
		public ObjectStatus Status;
		public double Budget;
	}

}
