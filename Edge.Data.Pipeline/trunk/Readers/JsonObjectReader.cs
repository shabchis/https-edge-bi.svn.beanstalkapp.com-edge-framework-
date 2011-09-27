using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace Edge.Data.Pipeline
{
	public class JsonObjectReader<T> : ReaderBase<T> where T:class
	{
		public Func<JsonTextReader,dynamic, T> OnObjectRequired = null;
		private string _url;
		private Stream _stream;
		private JsonTextReader _jsonTextReader;
		int _depth;
		public JsonObjectReader(string url,int depth)
		{
			_url = url;
			_depth = depth;

		}
		public JsonObjectReader(Stream stream, int depth)
		{
			_stream = stream;
			_depth = depth;

		}
		protected override void Open()
		{
			if (!string.IsNullOrEmpty(_url))
				_jsonTextReader = new JsonTextReader(new StreamReader(_url));
			else
				_jsonTextReader = new JsonTextReader(new StreamReader(_stream));
			
			
		}
		protected override bool Next(ref T next)
		{

			if (ReadUntilMatch())
			{
				next = OnObjectRequired(_jsonTextReader,null);
				return true;
			}
			else return false;

		}
		protected bool ReadUntilMatch()
		{
			bool returnValue = false;
			while (returnValue = _jsonTextReader.Read())
			{
				if (_jsonTextReader.TokenType == JsonToken.StartObject && _jsonTextReader.Depth == 3)
				{
					return true;					
				}
				else if (_jsonTextReader.TokenType == JsonToken.EndObject && _jsonTextReader.Depth == 3)
					continue;
			}
			return returnValue;

		}
		public override void Dispose()
		{
			if (_jsonTextReader != null)
				_jsonTextReader.Close();
		}
	}
}
