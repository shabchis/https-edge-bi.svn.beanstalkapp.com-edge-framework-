using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Objects
{
	public class ConstEdgeField
	{
		public string Name { get; set; }
		public object Value { get; set; }
		public Type Type { get; set; }
	}
}
