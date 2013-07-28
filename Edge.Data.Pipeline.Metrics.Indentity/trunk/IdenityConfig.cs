using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace Edge.Data.Pipeline.Metrics.Indentity
{
	[XmlRoot(ElementName = "IdentityConfig")]
	public class IdentityConfig
	{
		[XmlAttribute(AttributeName = "CreateNewObjects")]
		public bool CreateNewObjects { get; set; }

		[XmlAttribute(AttributeName = "UpdateExistingObjects")]
		public bool UpdateExistingObjects { get; set; }
		
		[XmlElement("EdgeType")]
		public List<EdgeTypeConfig> EdgeTypes { get; set; }

		public IdentityConfig()
		{
			CreateNewObjects = true;
			UpdateExistingObjects = true;
			EdgeTypes = new List<EdgeTypeConfig>();
		}

		#region Serialization
		public static string Serialize(IdentityConfig config)
		{
			var ser = new XmlSerializer(typeof(IdentityConfig));
			using (var textWriter = new StringWriter())
			{
				ser.Serialize(textWriter, config);
				return textWriter.ToString();
			}
		}

		public static IdentityConfig Deserialize(string xml)
		{
			var ser = new XmlSerializer(typeof(IdentityConfig));
			using (var reader = new StringReader(xml))
			{
				return ser.Deserialize(reader) as IdentityConfig;
			}
		} 
		#endregion
	}

	public class EdgeTypeConfig
	{
		[XmlAttribute(AttributeName = "Name")]
		public string Name { get; set; }

		[XmlAttribute(AttributeName = "CreateNewObjects")]
		public bool CreateNewObjects { get; set; }

		[XmlAttribute(AttributeName = "UpdateExistingObjects")]
		public bool UpdateExistingObjects { get; set; }
		
		[XmlElement("FieldToUpdate")]
		public List<FieldConfig> Fields { get; set; }

		public EdgeTypeConfig()
		{
			CreateNewObjects = true;
			UpdateExistingObjects = true;
			Fields = new List<FieldConfig>();
		}

		public string GetFieldList()
		{
			var fieldStr = new StringBuilder();
			foreach (var field in Fields)
			{
				fieldStr.AppendFormat("{0},", field.Name);
			}
			return fieldStr.Length > 0 ? fieldStr.Remove(fieldStr.Length - 1, 1).ToString() : string.Empty;
		}
	}

	public class FieldConfig
	{
		[XmlAttribute(AttributeName = "Name")]
		public string Name { get; set; }
	}
}
