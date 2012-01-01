using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GotDotNet.XPath;
using System.Xml;
using System.Dynamic;
using System.Collections;
using System.IO;

namespace Edge.Data.Pipeline
{
	public class XmlDynamicReader : XmlObjectReader<dynamic>
	{
		public XmlDynamicReader(string url, string xpath, XmlReaderSettings settings = null) : base(url, xpath, settings)
		{
			this.OnObjectRequired = ReadNode;
		}

		public XmlDynamicReader(Stream stream, string xpath, XmlReaderSettings settings = null) : base(stream, xpath, settings)
		{
			this.OnObjectRequired = ReadNode;
		}

		dynamic ReadNode(XmlReader reader)
		{
			dynamic xml = new XmlDynamicObject() { ArrayAddingMode = true };
			var xmlObject = (XmlDynamicObject)xml;

			string objectNodeName = GetNameWithPrefix(reader);
			int nodeDepth = reader.Depth;

			// Read attributes
			while (reader.MoveToNextAttribute())
			{
				string attributeName = GetNameWithPrefix(reader);
				if (xmlObject.Attributes == null)
					xmlObject.Attributes = new XmlDynamicObject();
				xmlObject.Attributes[attributeName] = reader.Value;
			}
			reader.MoveToElement();
			// Read value elements
			if (!reader.IsEmptyElement)
			{
				while (reader.Read())
				{
					string currentNodeName = GetNameWithPrefix(reader);
					if (reader.NodeType == XmlNodeType.EndElement && currentNodeName == objectNodeName && reader.Depth == nodeDepth)
					{
						break;
					}
					else if (reader.NodeType == XmlNodeType.Text)
					{
						if (xml.InnerText == null)
							xml.InnerText = string.Empty;
						xmlObject.InnerText += reader.Value;
					}
					else if (reader.NodeType == XmlNodeType.Element)
					{
						if (reader.Depth > nodeDepth)
							xml[currentNodeName] = reader.IsEmptyElement ? null : ReadNode(reader);
						else
							break;
					}

				}
			}
			xmlObject.ArrayAddingMode = false;

			// Decide what to return
			object returnObj;
			if (xmlObject.Values == null && xmlObject.Attributes == null)
			{
				// If nothing is defined inside this node, return null; if only the inner text, return a string
				if (xmlObject.InnerText == null)
					returnObj = null;
				else
					returnObj = xmlObject.InnerText;
			}
			else
			{
				// This node has children and/or attributes, return it as an object	
				returnObj = xmlObject;
			}

			return returnObj;
		}

		private static string GetNameWithPrefix(XmlReader reader)
		{
			return String.Format("{0}{1}", reader.Prefix == string.Empty ? string.Empty : reader.Prefix + ":", reader.Name);
		}
	}

	public class XmlDynamicObject:DynamicDictionaryObject
	{
		public dynamic Attributes { get; internal set; }
		public string InnerText { get; set; }
		public bool ArrayAddingMode = false;

		public XmlDynamicObject()
		{
			this.Attributes = null;
			this.InnerText = null;
		}

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

		/// <summary>
		/// Returns a list of child elements of the specified type.
		/// </summary>
		/// <param name="childName">
		///		The child element name to return.
		///		For example, in the XML <code><parent><child/></parent></code>, use parent.GetArray("child").
		///	</param>
		/// <returns></returns>
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
