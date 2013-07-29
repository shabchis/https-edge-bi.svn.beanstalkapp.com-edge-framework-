using System;
using System.Collections.Generic;
namespace Edge.Data.Objects
{
	public class Location : EdgeObject
	{
		public string Name { get; set; }
		public LocationType LocationType { get; set; }

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (LocationType != null) yield return new ObjectDimension
			{
				Field = EdgeType["LocationType"],
				Value = LocationType
			};
		}
	}

	public class LocationType : StringValue {}
}
