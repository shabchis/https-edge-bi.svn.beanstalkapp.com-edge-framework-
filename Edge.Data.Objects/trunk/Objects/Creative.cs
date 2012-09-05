using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract partial class Creative : EdgeObject
	{
	}

	public partial class CompositeCreative : Creative
	{
		public Dictionary<string, SingleCreative> ChildCreatives;
	}

	public abstract partial class SingleCreative : Creative
	{
	}

}
