using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Edge.Data.Objects
{
	public class DummyMapper
	{
		public Dictionary<Type, Dictionary<string, string>> Mapping;
		private Dictionary<string, string> EdgeObject;
		private Dictionary<string, string> TextCreative;
		private Dictionary<string, string> ImageCreative;
		private Dictionary<string, string> PlacementTarget;
		private Dictionary<string, string> KeywordTarget;
		private Dictionary<string, string> GenderTarget;
		private Dictionary<string, string> AgeGroupTarget;
		private Dictionary<string, string> Segment;
		private Dictionary<string, string> Campaign;
		private Dictionary<string, string> Ad; 

		public DummyMapper()
		{
			Mapping = new Dictionary<Type, Dictionary<string, string>>();

			EdgeObject = new Dictionary<string, string>()
			{
				{"GK","GK"},
				{"Name","Name"},
				{"OriginalID","OriginalID"},
				{"AccountID","AccountID"},
				{"Status","Status"}
			};

			Mapping.Add(typeof(Edge.Data.Objects.EdgeObject), EdgeObject);

			KeywordTarget = new Dictionary<string, string>()
			{
				{"MatchType","int_Field1"}
			};
			Mapping.Add(typeof(Edge.Data.Objects.KeywordTarget), KeywordTarget);

			TextCreative = new Dictionary<string, string>()
			{
				{"TextType","int_Field1"},
				{"Text","string_Field1"},
				{"Text2","string_Field2"}
			};
			Mapping.Add(typeof(Edge.Data.Objects.TextCreative), TextCreative);

			ImageCreative = new Dictionary<string, string>()
			{
				{"ImageUrl","string_Field1"},
				{"ImageSize","string_Field2"}
			};
			Mapping.Add(typeof(Edge.Data.Objects.ImageCreative), ImageCreative);

			Campaign = new Dictionary<string, string>()
			{
				{"Budget","int_Field1"}
			};
			Mapping.Add(typeof(Edge.Data.Objects.Campaign), Campaign);

			Ad = new Dictionary<string, string>()
			{
				{"DestinationUrl","string_Field1"}
			};
			Mapping.Add(typeof(Edge.Data.Objects.Ad), Ad);

			Segment = new Dictionary<string, string>()
			{
				{"MetaPropertyID","int_Field1"}
			};
			Mapping.Add(typeof(Edge.Data.Objects.Segment), Segment);
		}

		public string GetMap(Type type, string Name)
		{
			string map;
			Mapping[type].TryGetValue(Name, out map);

			if (string.IsNullOrEmpty(map))
				map = EdgeObject[Name];

			return map;

		}
	}
}
