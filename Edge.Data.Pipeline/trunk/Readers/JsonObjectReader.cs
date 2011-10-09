using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace Edge.Data.Pipeline
{
	public class JsonObjectReader<T> : ReaderBase<T> where T : class
	{
		public Func<JsonTextReader, T> OnObjectRequired = null;
		private string _url;
		private Stream _stream;
		private JsonTextReader _jsonTextReader;
		protected string _jsonPath;
		protected int? _depth;
		protected string _neededPropery;
		protected JsonToken _neededToken;
		protected int _currentPathIndex = 0;
		protected string[] _jsonPathSegments;
		public JsonObjectReader(string url, string jsonPath)
		{
			_url = url;
			_jsonPath = jsonPath;

		}
		public JsonObjectReader(Stream stream, string jsonPath)
		{
			_stream = stream;
			_jsonPath = jsonPath;

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
			if (OnObjectRequired == null)
				throw new InvalidOperationException("A delegate must be specified for OnObjectRequired.");

			if (ReadUntilMatch())
			{
				next = OnObjectRequired(_jsonTextReader);
				return next != null;
			}
			else
				return false;
		}

		
		

		protected bool ReadUntilMatch()
		{
			if (_depth == null)
			{
				Normalize();
				while (_jsonTextReader.Read())
				{
					string curPathSegment = _jsonPathSegments[_currentPathIndex];

					if (curPathSegment == "[*]")
					{
						if (_jsonTextReader.TokenType == JsonToken.StartArray)
						{
							if (_currentPathIndex == _jsonPathSegments.Length - 1)
							{
								// TODO: WE FOUND IT, return true and deserialize the contents
								_neededToken = _jsonTextReader.TokenType;
								_depth = _jsonTextReader.Depth;
								return true;
							}
							else
								_currentPathIndex++;
						}
					}
					if (
						(curPathSegment == "$" && _jsonTextReader.Depth == 0) ||
						((_jsonTextReader.Value != null && curPathSegment == _jsonTextReader.Value.ToString()) && _jsonTextReader.TokenType == JsonToken.PropertyName)
						)
					{
						if (_currentPathIndex == _jsonPathSegments.Length - 1)
						{
							// TODO: WE FOUND IT, return true and deserialize the contents
							_neededToken = _jsonTextReader.TokenType;
							_depth = _jsonTextReader.Depth;
							if (_jsonTextReader.TokenType == JsonToken.PropertyName)
								_neededPropery = _jsonTextReader.Value.ToString();
							return true;
						}
						else
							_currentPathIndex++;
					}
					if (curPathSegment == "*")
					{
						if (_currentPathIndex == _jsonPathSegments.Length - 1)
						{
							// TODO: WE FOUND IT, return true and deserialize the contents
							_neededToken = _jsonTextReader.TokenType;
							_depth = _jsonTextReader.Depth;
							return true;
						}
						else
							_currentPathIndex++;
					}
				}
			}
			else
			{
				if (_jsonTextReader.Read())
				{
					if (_jsonTextReader.Depth < _depth)
					{
						if (_neededToken == JsonToken.PropertyName && _jsonPathSegments[_jsonPathSegments.Length - 2] != "[*]"
							&& _jsonPathSegments[_jsonPathSegments.Length - 2] != "*"
							)
						{
							while (_jsonTextReader.Read())
							{
								if (_jsonTextReader.Depth == _depth && _jsonTextReader.TokenType == JsonToken.PropertyName && _jsonTextReader.Value != null && _jsonTextReader.Value.ToString() == _neededPropery)
									return true;

							}

						}

					}
					else
					{
						if (_jsonTextReader.Depth == _depth && _jsonTextReader.TokenType == _neededToken)
						{
							
							if (_neededToken == JsonToken.PropertyName)
							{
								if (!string.IsNullOrEmpty( _neededPropery))
								{
									while (_jsonTextReader.Read())
									{
										if (_jsonTextReader.Depth == _depth && _jsonTextReader.TokenType == JsonToken.PropertyName && _jsonTextReader.Value != null && _jsonTextReader.Value.ToString() == _neededPropery)
											return true;
									}
								}
								else
									return true;
							}
							else
								return true;
						}
					}
				}
			}

			return false;
		}

		private void Normalize()
		{
			List<string> segments = _jsonPath.Split('.').ToList();
			List<int> indexs = new List<int>();
			for (int i = 0; i < segments.Count; i++)
			{
				if (segments[i].Contains("[*]"))
				{
					indexs.Add(i);
				}
			}
			foreach (var index in indexs)
			{
				segments[index] = segments[index].Replace("[*]", string.Empty);
				segments.Insert(index + 1, "[*]");
			}
			_jsonPathSegments = segments.ToArray();
		}

		//protected bool ReadUntilMatch()
		//{
		//    // split json path
		//    string[] strArray = JsonPath.JsonPathContext.Normalize(_jsonPath).Split(';');
		//    bool returnObject = false;

		//    if (_depth == null)
		//    {

		//        for (int i = 0; i < strArray.Length; i++)
		//        {
		//            string sign = strArray[i];
		//            switch (sign)
		//            {
		//                case "$":
		//                    {
		//                        returnObject = _jsonTextReader.Read();
		//                        _depth = _jsonTextReader.Depth;
		//                        break;
		//                    }
		//                case "*":
		//                    {
		//                        while (_jsonTextReader.TokenType != JsonToken.StartObject)
		//                        {
		//                            returnObject = _jsonTextReader.Read();

		//                        }
		//                        _depth = _jsonTextReader.Depth;
		//                        break;

		//                    }
		//                default:
		//                    {
		//                        while (returnObject = _jsonTextReader.Read())
		//                        {
		//                            if (_jsonTextReader.Value != null && _jsonTextReader.Value.ToString() == strArray[i])
		//                            {
		//                                _depth = _jsonTextReader.Depth;
		//                                break;
		//                            }
		//                            else if (i <= strArray.Length - 1)
		//                            {
		//                                returnObject = false;
		//                            }
		//                        }
		//                        break;

		//                    }
		//            }

		//        }
		//    }
		//    else if (strArray[strArray.Length - 1] != "$" && strArray[strArray.Length - 1] != "*")
		//    {
		//        string propertyName = strArray[strArray.Length - 1];
		//        while (returnObject = _jsonTextReader.Read())
		//        {
		//            if (_jsonTextReader.Value == propertyName)
		//                break;
		//        }
		//    }
		//    else
		//    {
		//        while (returnObject=_jsonTextReader.Read())
		//        {

		//            if (_depth == _jsonTextReader.Depth)
		//                break;
		//        }
		//    }



		//    return returnObject;


		//}
		public override void Dispose()
		{
			if (_jsonTextReader != null)
				_jsonTextReader.Close();
		}
	}
}
