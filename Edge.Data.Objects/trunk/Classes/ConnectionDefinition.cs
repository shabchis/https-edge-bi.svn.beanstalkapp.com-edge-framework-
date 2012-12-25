using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class ConnectionDefinition
	{
		public int ID;
		public string Name;
		public Account Account;
		public Channel Channel;
		public EdgeType EdgeType;
		//public ConnectionOptions Options;
	}

	/*
	[Flags]
	public enum ConnectionOptions
	{
		None = 0x0,
		All = 0xff
	}
	*/

}
