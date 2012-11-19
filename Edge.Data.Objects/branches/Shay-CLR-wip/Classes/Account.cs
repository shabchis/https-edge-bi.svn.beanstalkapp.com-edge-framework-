using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class Account
	{
		public int ID;
		public string Name;
		public Account ParentAccount;
	}

}
