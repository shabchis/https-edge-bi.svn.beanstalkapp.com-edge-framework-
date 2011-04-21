using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	public class Creative
	{
		public string OriginalID;
		public CreativeType CreativeType;
		public string Value;
	}

	public enum CreativeType
	{
		Title=1,
		Body=2,
		Image=3,
		DisplayUrl=3
	}
}
