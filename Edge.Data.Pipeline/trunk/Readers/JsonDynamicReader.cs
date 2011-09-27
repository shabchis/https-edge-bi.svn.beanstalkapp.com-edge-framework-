using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace Edge.Data.Pipeline
{
	public class JsonDynamicReader : JsonObjectReader<dynamic>
	{
		public JsonDynamicReader(string url,int depth) : base(url,depth)
		{
			this.OnObjectRequired = ReadNode;
		}

		public JsonDynamicReader(Stream stream,int depth)
			: base(stream,depth)
		{
			this.OnObjectRequired = ReadNode;
		}
		dynamic ReadNode(JsonTextReader reader,dynamic o=null)
		{
			dynamic obj;
			List<string> tokenType = new List<string>();
			if (o==null)
			obj = new DynamicDictionaryObject();
			else
				obj=o;
			while (reader.Read())
			{


				if (reader.TokenType == JsonToken.StartObject)
				{

					continue;
				}
				else if (reader.TokenType == JsonToken.EndObject)
				{
					if (tokenType.Count > 0)
						tokenType.RemoveAt(tokenType.Count - 1);
					if (reader.Depth == 3)
						return obj;
				}
				else if (reader.TokenType == JsonToken.PropertyName)
				{

					tokenType.Add(reader.Value.ToString());

				}
				else if (reader.TokenType == JsonToken.StartArray)
				{
					ReadArray(ref reader, ref obj, tokenType);
					tokenType.RemoveAt(tokenType.Count - 1);



				}
				else if (reader.TokenType == JsonToken.StartObject)
					ReadNode(reader,  obj);
				else
				{
					StringBuilder strToken = new StringBuilder();
					for (int i = 0; i < tokenType.Count; i++)
						strToken.Append(i == tokenType.Count - 1 ? tokenType[i] : string.Format("{0}.", tokenType[i]));
					obj[strToken.ToString()]= reader.Value == null ? null : reader.Value.ToString();
					tokenType.RemoveAt(tokenType.Count - 1);
				}


			}
			return obj;
		}

		private void ReadArray(ref JsonTextReader reader, ref dynamic obj, List<string> tokenType)
		{
			List<object> arr = new List<object>();
			while (reader.Read())
			{
				if (reader.TokenType == JsonToken.EndArray)
				{

					StringBuilder strToken = new StringBuilder();
					for (int i = 0; i < tokenType.Count; i++)
						strToken.Append(i == tokenType.Count - 1 ? tokenType[i] : string.Format("{0}.", tokenType[i]));
					obj[strToken.ToString()]= reader.Value == null ? null : reader.Value.ToString();
					return;
				}
				else
				{
					if (reader.Value != null)
						arr.Add(reader.Value.ToString());
				}
			}
		}
	}
}
