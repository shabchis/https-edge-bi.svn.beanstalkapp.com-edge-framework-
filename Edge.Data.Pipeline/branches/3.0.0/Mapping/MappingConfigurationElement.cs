using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Xml;
using System.IO;
using Edge.Core.Services.Configuration;

namespace Edge.Data.Pipeline.Mapping
{
	[Obsolete("This class is preserved for backwards compatibility with Edge.Services.config file only.")]
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
				reader.ReadToFollowing(MappingConfigurationElement.ExtensionName);
				string path = reader.GetAttribute("Path");
				if (!string.IsNullOrEmpty(path))
					//mapping.Load(Path.IsPathRooted(path) ? path :  Path.Combine(Environment.CurrentDirectory, path));
					mapping.Load(path);
				else
					mapping.Load(reader);
			}
		}
	}

}
