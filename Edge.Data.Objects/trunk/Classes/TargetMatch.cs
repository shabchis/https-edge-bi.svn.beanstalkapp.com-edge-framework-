using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class TargetMatch : EdgeObject
	{
		public Target Target;
		public string DestinationUrl;

		public override IEnumerable<EdgeObject> GetChildObjects()
		{
			yield return Target;
		}
	}

}
