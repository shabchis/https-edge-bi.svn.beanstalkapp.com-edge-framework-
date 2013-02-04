using System.Collections.Generic;

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

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			if (Target != null) yield return new ObjectDimension {Value = Target};
		}
	}
}
