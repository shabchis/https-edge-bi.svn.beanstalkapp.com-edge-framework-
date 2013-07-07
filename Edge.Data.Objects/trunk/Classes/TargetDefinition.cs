using System;
using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class TargetDefinition : EdgeObject
	{
		public EdgeObject Parent;
		public Target Target;
		//public string DestinationUrl;
		public Destination Destination;
		
		public override bool HasChildsObjects
		{
			get { return Target != null; }
		}

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (Destination != null) yield return new ObjectDimension
			{
				Field = EdgeType["Destination"],
				Value = Destination
			};

			if (Target != null) yield return new ObjectDimension 
			{
				Field = this.EdgeType[String.Format("{0}_Target", this.EdgeType.Name)],
				Value = Target
			};
		}
	}
}
