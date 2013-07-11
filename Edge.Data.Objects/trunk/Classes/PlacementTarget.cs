using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class PlacementTarget : Target
	{
		public string Value;
		public PlacementType PlacementType;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (PlacementType != null) yield return new ObjectDimension
			{
				Field = EdgeType["PlacementType"],
				Value = PlacementType
			};
		}
	}

	public class PlacementType : StringValue { }

	//public enum PlacementType
	//{
	//	Unidentified = 0,
	//	Automatic = 4,
	//	Managed = 5
	//}
}
