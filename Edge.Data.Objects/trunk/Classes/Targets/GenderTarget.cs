using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[TableInfo(Name = "TargetGender")]
	public partial class GenderTarget : Target
	{
		public Gender Gender;
	}

	public enum Gender
	{
		Unspecified = 0,
		Male = 1,
		Female = 2,
		Other = 3
	}

}
