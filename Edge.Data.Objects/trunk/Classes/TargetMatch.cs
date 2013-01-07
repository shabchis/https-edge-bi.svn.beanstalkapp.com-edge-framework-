using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class TargetMatch : EdgeObject
	{
		public EdgeObject Parent;
		public Target Target;
		public TargetDefinition TargetDefinition;
		public string DestinationUrl;
		
		public override bool HasChildsObjects
		{
			get { return Target != null || TargetDefinition != null; }
		}

		public override IEnumerable<EdgeObject> GetChildObjects()
		{
			if (Target != null)
				yield return Target;
			if (TargetDefinition != null)
				yield return TargetDefinition;

			yield break;
		}
	}

}
