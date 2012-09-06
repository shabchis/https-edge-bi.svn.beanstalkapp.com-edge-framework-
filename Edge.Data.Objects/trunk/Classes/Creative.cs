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

		public override IEnumerable<EdgeObject> GetChildObjects()
		{
			foreach (var pair in ChildCreatives.OrderBy(p => p.Key))
				yield return pair.Value;
		}
	}

	public abstract partial class SingleCreative : Creative
	{
	}

}
