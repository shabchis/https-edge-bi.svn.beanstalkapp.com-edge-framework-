using System.Collections.Generic;

namespace Edge.Data.Objects
{
	public partial class GenderTarget : Target
	{
		public Gender Gender;

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (Gender != null) yield return new ObjectDimension
			{
				Field = EdgeType["Gender"],
				Value = Gender
			};
		}
	}

	public class Gender : StringValue {}

	//public enum Gender
	//{
	//	Unspecified = 0,
	//	Male = 1,
	//	Female = 2,
	//	Other = 3
	//}

}
