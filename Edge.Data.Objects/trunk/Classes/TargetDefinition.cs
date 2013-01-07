using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class TargetDefinition : EdgeObject
	{
		public EdgeObject Parent;
		public Target Target;
		public string DestinationUrl;
		
		public override bool HasChildsObjects
		{
			get { return Target != null; }
		}

		public override IEnumerable<EdgeObject> GetChildObjects()
		{
			if (Target != null)
				yield return Target;

			yield break;
		}
	}

}
