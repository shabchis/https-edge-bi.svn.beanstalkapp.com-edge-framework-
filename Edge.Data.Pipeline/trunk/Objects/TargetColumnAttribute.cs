using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	class TargetColumnAttribute : Attribute
	{
		internal int TargetColumnID;
		public TargetColumnAttribute(int targetColumnID)
		{
			TargetColumnID = targetColumnID;
		}
	}
}
