using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GotDotNet.XPath;
using System.Xml;
using System.Dynamic;
using System.Collections;

namespace Edge.Data.Pipeline.Readers
{
	public class XmlDynamicReader : XmlObjectReader<dynamic>
	{
		public XmlDynamicReader(string url, string xpath, string[] treatAsArrayXPaths = null) : base(url, xpath)
		{
			this.OnObjectRequired = ReadNode;
		}

		dynamic ReadNode(XmlReader reader)
		{
			dynamic xml = new XmlDynamicObject() { ArrayAddingMode = true };
			var xmlObject = (XmlDynamicObject)xml;

			string nodeName = reader.Name;
			int nodeDepth = reader.Depth;

			// Read attributes
			while (reader.MoveToNextAttribute())
			{
				if (xmlObject.Attributes == null)
					xmlObject.Attributes = new object();
				xmlObject.Attributes[reader.Name] = reader.Value;
			}

			// Read value elements
			while (reader.Read())
			{
				if (reader.NodeType == XmlNodeType.EndElement && reader.Name == nodeName && reader.Depth == nodeDepth)
				{
					break;
				}
				else if (reader.NodeType == XmlNodeType.Element)
				{
					xml[reader.Name] = ReadNode(reader);
				}
				else if (reader.NodeType == XmlNodeType.Text)
				{
					if (xml.InnerText == null)
						xml.InnerText = string.Empty;
					xmlObject.InnerText += reader.Value;
				}
			}
			xmlObject.ArrayAddingMode = false;

			// Decide what to return
			object returnObj;
			if (xmlObject.Children == null && xmlObject.Attributes == null)
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
	}

	public class XmlDynamicObject:DynamicObject
	{
		public dynamic Attributes { get; internal set; }
		public string InnerText { get; set; }
		public bool ArrayAddingMode = false;
		public bool CaseSensitive = true;
		internal Dictionary<string, object> Children = null;

		public XmlDynamicObject()
		{
			this.Attributes = null;
			this.InnerText = null;
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			string name = CaseSensitive ? binder.Name : binder.Name.ToLower();
			return Children.TryGetValue(name, out result);
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			if (Children == null)
				Children = new Dictionary<string, object>();

			string name = CaseSensitive ? binder.Name : binder.Name.ToLower();
			object current;
			if (ArrayAddingMode && Children.TryGetValue(name, out current))
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
					Children[name] = list;
				}
			}
			else
			{
				Children[name] = value;
			}
			return true;
		}
	}

}
