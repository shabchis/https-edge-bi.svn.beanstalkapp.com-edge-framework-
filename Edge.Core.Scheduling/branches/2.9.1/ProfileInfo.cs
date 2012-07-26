using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Scheduling
{
	public class ProfileInfo
	{
		public int AccountID { get; set; }
		public string AccountName { get; set; }
		public List<string> Services = new List<string>();
	}
}
