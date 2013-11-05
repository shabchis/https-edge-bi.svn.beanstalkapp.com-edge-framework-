using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Dynamic;
using System.Collections;

namespace Edge.Data.Pipeline
{
	public class JsonDynamicReader : JsonObjectReader<dynamic>
	{
		public JsonDynamicReader(string url, string jsonPath)
			: base(url, jsonPath)
		{
			this.OnObjectRequired = ReadNode;
		}

		public JsonDynamicReader(Stream stream, string jsonPath)
			: base(stream, jsonPath)
		{
			this.OnObjectRequired = ReadNode;
		}
		dynamic ReadNode(JsonTextReader reader)
		{
			dynamic obj = new JsonDynamicObject();
		
			
			//this what we ar looking for
		
			string PropertyName = string.Empty;
			bool exit = false;

			
			while (!exit)
			{
				if (reader.Depth<_depth)
				{
					exit=true;
					break;
				}
			    switch (_neededToken)
			    {
			        case JsonToken.EndObject:
						{
							exit=true;
			            break;
						}
			        case JsonToken.PropertyName:
			            {
						
			               PropertyName=reader.Value.ToString();
						   reader.Read();
						   object o;
						   switch (reader.TokenType)
						   {
							  
							   case JsonToken.StartArray:
								   o = GetArray(reader);
								   break;							   
							   case JsonToken.StartObject:
								   o = GetObject(reader);
								   break;							  
							   default:
								   o = GetValue(reader);
								   break;
						   }
						   obj[PropertyName] = o;
						   exit = true;
			                break;
			            }
			        case JsonToken.StartArray:
			            {
							obj["array"]= GetArray(reader);
							exit = true;
			                break;
			            }
					case JsonToken.EndArray:
						{
							exit = true;
							break;
						}
					case JsonToken.StartObject:
						{
							foreach (KeyValuePair<string,object> keyVal in GetObject(reader))
							{
								obj[keyVal.Key] = keyVal.Value;
								
							}
							
							exit = true;
							break;
						}
			        default:
			            {
							obj[PropertyName]= GetValue(reader);
							exit = true;
			                break;
			            }
			    }



			}
		

			return (JsonDynamicObject)obj;

			

		}



		
		private static dynamic GetObject(JsonTextReader reader)
		{
			Dictionary<string, object> returnObject = new Dictionary<string, object>();
			object obj;
			List<object> arr = new List<object>();
			string property=string.Empty;
			while (reader.Read())
			{
				if (reader.TokenType == JsonToken.PropertyName)
					property = GetProperty(reader);
				else if (reader.TokenType == JsonToken.StartObject)
				{
					obj = GetObject(reader);
					returnObject[property] = obj;
				}
				else if (reader.TokenType == JsonToken.EndObject)
					return returnObject;
				else if (reader.TokenType == JsonToken.StartArray)
				{
					arr = GetArray(reader);
					returnObject[property] = arr;
				}
				else
				{
					returnObject[property] = GetValue(reader);
				}
			}
			return returnObject;
		}

		private static List<object> GetArray(JsonTextReader reader)
		{
			List<object> objects = new List<object>();
			while (reader.Read())
			{
				if (reader.TokenType == JsonToken.StartObject)
					objects.Add(GetObject(reader));
				else if (reader.TokenType == JsonToken.EndArray)
					break;
				else
					objects.Add(GetValue(reader));

			}
			return objects;
		}

		private static string GetValue(JsonTextReader reader)
		{
			string returnValue = string.Empty;
			if (reader.Value!=null)
				returnValue=reader.Value.ToString();

			return returnValue;
		}

		private static string GetProperty(JsonTextReader reader)
		{
			return reader.Value.ToString();
		}


	}
	public class JsonDynamicObject : DynamicDictionaryObject
	{
		public bool ArrayAddingMode = false;
		
		protected override bool SetMemberInternal(string name, object value)
		{
			object current;
			if (this.ArrayAddingMode && this.Values.TryGetValue(name, out current))
			{
				// In array adding mode, either expand an existing list or convert a value to a list
				if (current is IList)
				{
					IList list = (IList)current;
					list.Add(value);
				}
				else
				{
					List<object> list = new List<object>();
					list.Add(current);
					list.Add(value);
					this.Values[name] = list;
				}

				// Custom handling
				return true;
			}
			else
			{
				// Default handling
				return false;
			}
		}

		public object GetMemberByPath(string path)
		{
			throw new NotImplementedException();
		}

		public object[] GetArray(string childName)
		{
			object child;
			object[] returnArray;

			if (!this.Values.TryGetValue(childName, out child))
			{
				returnArray = new object[0];
			}
			else
			{
				if (child is IList)
				{
					IList list = (IList)child;
					returnArray = new object[list.Count];
					list.CopyTo(returnArray, 0);
				}
				else
				{
					returnArray = new object[] { child };
				}
			}

			return returnArray;
		}
	}
}
