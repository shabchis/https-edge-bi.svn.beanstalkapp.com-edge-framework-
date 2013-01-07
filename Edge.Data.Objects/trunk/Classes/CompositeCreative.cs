using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class CompositeCreative : Creative
	{
		public Dictionary<CompositePartField, SingleCreative> Parts;
	}
}
