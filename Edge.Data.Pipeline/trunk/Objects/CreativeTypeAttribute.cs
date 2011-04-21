using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	class CreativeTypeAttribute : Attribute
	{
		internal int CreateiveID;
		public CreativeTypeAttribute(int createiveID)
		{
			CreateiveID = createiveID;
		}
	}
}
