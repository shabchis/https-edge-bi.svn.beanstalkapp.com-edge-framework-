using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class DummyMapper
	{
		public static Dictionary<Type, Dictionary<string, string>> Mapping;
		public static Dictionary<string, string> EdgeObject;
		public static Dictionary<string, string> TextCreative;
		public static Dictionary<string, string> ImageCreative;
		public static Dictionary<string, string> PlacementTarget;
		public static Dictionary<string, string> KeywordTarget;
		public static Dictionary<string, string> GenderTarget;
		public static Dictionary<string, string> AgeGroupTarget;

		public DummyMapper()
		{
			Mapping = new Dictionary<Type, Dictionary<string, string>>();

			EdgeObject = new Dictionary<string, string>()
			{
				{"Name","Name"},
				{"OriginalID","OriginalID"},
				{"AccountID","AccountID"},
				{"Status","Status"}
			};
			Mapping.Add(typeof(Edge.Data.Objects.EdgeObject), EdgeObject);

			TextCreative = new Dictionary<string, string>()
			{
				{"TextType","int_Field1"},
				{"Text","string_Field1"},
				{"Text2","string_Field2"}
			};
			Mapping.Add(typeof(Edge.Data.Objects.TextCreative), EdgeObject);

			ImageCreative = new Dictionary<string, string>()
			{
				{"ImageUrl","string_Field1"},
				{"ImageSize","string_Field2"}
			};
			Mapping.Add(typeof(Edge.Data.Objects.ImageCreative), EdgeObject);
			
		}

	}
}
