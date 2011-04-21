using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	class TargetTypeAttribute : Attribute
	{
		internal int TargetTypeID;
		public TargetTypeAttribute(int targetTypeID)
		{
			TargetTypeID = targetTypeID;
		}
	}
}
