using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[TableInfo(Name = "TargetMatch")]
	public partial class TargetMatch : EdgeObject
	{
		public Target Target;
		public string DestinationUrl;
		public override bool HasChildsObjects
		{
			get
			{
				if (Target != null)
					return true;
				else
					return false;
			}
		}

		public override IEnumerable<EdgeObject> GetChildObjects()
		{
			yield return Target;
		}
	}

}
