using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	class CreativeColumnAttribute : Attribute
	{
		internal int  CreativeColumnID;
		public CreativeColumnAttribute(int creativeColumnID)
		{
			CreativeColumnID = creativeColumnID;
		}
	}
}
