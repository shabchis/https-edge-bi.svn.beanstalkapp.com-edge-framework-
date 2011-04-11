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

		XmlDynamicObject ReadNode(XmlReader reader)
		{
			dynamic xml = new XmlDynamicObject() { ArrayAddingMode = true };

			string nodeName = reader.Name;
			int nodeDepth = reader.Depth;

			// Read attributes
			while (reader.MoveToNextAttribute())
				xml.Attributes[reader.Name] = reader.Value;

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
					xml.InnerText += reader.Value;
				}
			}

			((XmlDynamicObject)xml).ArrayAddingMode = false;
			return xml;
		}
	}

	public class XmlDynamicObject:DynamicObject
	{
		public dynamic Attributes { get; private set; }
		public string InnerText { get; set; }

		Dictionary<string, object> _children = new Dictionary<string, object>();
		public bool ArrayAddingMode = false;
		public bool CaseSensitive = true;

		public XmlDynamicObject()
		{
			this.Attributes = new object();
			this.InnerText = string.Empty;
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			string name = CaseSensitive ? binder.Name : binder.Name.ToLower();
			return _children.TryGetValue(name, out result);
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			string name = CaseSensitive ? binder.Name : binder.Name.ToLower();
			object current;
			if (ArrayAddingMode && _children.TryGetValue(name, out current))
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
					_children[name] = list;
				}
			}
			else
			{
				_children[name] = value;
			}
			return true;
		}
	}

}
