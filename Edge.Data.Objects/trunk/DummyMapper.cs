using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class DummyMapper
	{
		public static Dictionary<object, string> DefaultMapping;
		public static Dictionary<object, string> TextCreative;
		public static Dictionary<object, string> ImageCreative;
		public static Dictionary<object, string> PlacementTarget;
		public static Dictionary<object, string> KeywordTarget;
		public static Dictionary<object, string> GenderTarget;
		public static Dictionary<object, string> AgeGroupTarget;

		public DummyMapper()
		{
			DefaultMapping = new Dictionary<object, string>()
			{
				{"Name","Name"},
				{"OriginalID","OriginalID"},
				{"AccountID","AccountID"},
				{"Status","Status"}
			};

			TextCreative = new Dictionary<object, string>()
			{
				{"TextType","int_Field1"},
				{"Text","string_Field1"},
				{"Text2","string_Field2"}
			};

			ImageCreative = new Dictionary<object, string>()
			{
				{"ImageUrl","string_Field1"},
				{"ImageSize","string_Field2"}
			};
		}

	}
}
