using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class CompositeCreative : Creative
	{
		public Dictionary<string, SingleCreative> Parts;

		public override IEnumerable<EdgeObject> GetChildObjects()
		{
			foreach (var pair in Parts.OrderBy(p => p.Key))
				yield return pair.Value;
		}
		public override bool HasChildsObjects
		{
			get
			{
				if (Parts != null && Parts.Count > 0)
					return true;
				else
					return false;
			}
		}
	}
}
