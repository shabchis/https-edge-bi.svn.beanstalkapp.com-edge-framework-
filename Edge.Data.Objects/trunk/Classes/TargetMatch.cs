using System;
using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class TargetMatch : EdgeObject
	{
		public EdgeObject Parent;
		public Target Target;
		public TargetDefinition TargetDefinition;
		//public string DestinationUrl;
		public Destination Destination;
		
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

			if (Target != null) yield return new ObjectDimension
			{
				Field = EdgeType[String.Format("{0}_Target", EdgeType.Name)],
				Value = Target
			};

			if (Destination != null) yield return new ObjectDimension
			{
				Field = EdgeType["Destination"],
				Value = Destination
			};

			if (TargetDefinition != null) yield return new ObjectDimension 
			{
				Field = EdgeType[String.Format("{0}_TargetDefinition", EdgeType.Name)],
				Value = TargetDefinition
			};
		}
	}

}
