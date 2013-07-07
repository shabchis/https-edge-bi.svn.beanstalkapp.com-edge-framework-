using System;
using System.Collections.Generic;
namespace Edge.Data.Objects
{
	public partial class TextCreative : SingleCreative
	{
		//public TextCreativeType TextCreativeType;
		public string Text;

	//	public override IEnumerable<ObjectDimension> GetObjectDimensions()
	//	{
	//		foreach (var dimension in base.GetObjectDimensions())
	//		{
	//			yield return dimension;
	//		}
	//		if (TextCreativeType != null) yield return new ObjectDimension
	//		{
	//			Field = EdgeType["TextCreativeType"],
	//			Value = TextCreativeType
	//		};
	//	}
	}

	public class TextCreativeType : StringValue { }
	
	//public enum TextCreativeType
	//{
	//	Text = 1,
	//	Url = 2
	//}
}
