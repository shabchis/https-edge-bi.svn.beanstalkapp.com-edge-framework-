using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[TableInfo(Name = "TargetAge")]
	public partial class AgeTarget : Target
	{
		public int FromAge;
		public int ToAge;
	}

}
