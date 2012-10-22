using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[TableInfo(Name = "PropertyOption")]
	public partial class PropertyOption : ChannelSpecificObject
	{
		public MetaProperty MetaProperty;

	}
}
