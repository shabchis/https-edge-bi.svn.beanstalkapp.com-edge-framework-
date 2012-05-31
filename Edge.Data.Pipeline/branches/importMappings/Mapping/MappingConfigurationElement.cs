using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using Edge.Core.Configuration;
using System.Xml;
using System.IO;

namespace Edge.Data.Pipeline.Mapping
{
	public class MappingConfigurationElement : ConfigurationElement, ISerializableConfigurationElement
	{
		public const string ExtensionName = "Mappings";

		public string RawXml { get; private set; }

		void ISerializableConfigurationElement.Deserialize(XmlReader reader)
		{
			this.RawXml = reader.ReadOuterXml();
		}

		void ISerializableConfigurationElement.Serialize(XmlWriter writer, string elementName)
		{
			writer.WriteRaw(this.RawXml);
		}

		public void LoadInto(MappingConfiguration mapping)
		{
			using (XmlTextReader reader = new XmlTextReader(new StringReader(this.RawXml)))
			{
				string path = reader.GetAttribute("Path");
				if (!string.IsNullOrEmpty(path))
					mapping.Load(path);
				else
					mapping.Load(reader);
			}
		}
	}

}
