using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[TableInfo(Name = "Segment")]
	public partial class Segment : ChannelSpecificObject
	{
		public ConnectionDefinition ConnectionDefinition;
		public string Value;

	}
}
