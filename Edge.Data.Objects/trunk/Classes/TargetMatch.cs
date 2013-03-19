using System;
using System.Collections.Generic;

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

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}
			if (Target != null) yield return new ObjectDimension {Value = Target};
			if (TargetDefinition != null) yield return new ObjectDimension 
			{
				Field = this.EdgeType[String.Format("{0}_TargetDefinition", this.EdgeType.Name)],
				Value = TargetDefinition
			};
		}
	}

}
