using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Configuration;
using System.Threading;
using System.Text.RegularExpressions;
using System.Data;
using System.Reflection;
using System.IO;
using System.Xml;
using System.Xml.Schema;

namespace Eggplant.Persistence.Providers.Xml
{
	public class XmlProvider : PersistenceProvider
	{
		/*public readonly Stream Stream = null;*/
		public readonly string Url = null;
		public readonly string SchemaUrl = null;
		private readonly XmlReaderSettings _xmlReaderSettings;

		public XmlProvider(string url, string schemaUrl)
		{
			Url = url;
			SchemaUrl = schemaUrl;

			// Settings for retrieving schema information
			_xmlReaderSettings = new XmlReaderSettings()
			{
				IgnoreComments = true,
				IgnoreWhitespace = true,
				IgnoreProcessingInstructions = true/*,
				ValidationType = ValidationType.Schema,
				ValidationFlags =
					XmlSchemaValidationFlags.ProcessInlineSchema |
					XmlSchemaValidationFlags.ReportValidationWarnings,
				XmlResolver = new XsdResolver()
												    */

			};
			//_xmlReaderSettings.ValidationEventHandler += new ValidationEventHandler(ValidationEventHandler);

			LoadMappingsFromXsd(schemaUrl);
		}

		void ValidationEventHandler(object sender, ValidationEventArgs e)
		{
			// TODO: handle valdiation errors
			throw new NotImplementedException();
		}

		internal void LoadMappingsFromXsd(string schemalUrl)
		{
			XmlReader reader = XmlReader.Create(schemalUrl, _xmlReaderSettings);

			while (!reader.EOF)
			{
				reader.Read();

				if (reader.NodeType == XmlNodeType.Element)
				{
					if (attribute == reader.NameTable.Get("complexType"))
					while (reader.MoveToNextAttribute())
					{
						string attribute = reader.Name;
						
					}
				}
			}
		}

		protected override PersistenceConnection CreateNewConnection()
		{
			return new XmlConnection(this);
		}
	}

	class XsdResolver : XmlUrlResolver
	{
		public override Uri ResolveUri(Uri baseUri, string relativeUri)
		{
			return base.ResolveUri(baseUri, relativeUri);
		}
	}

}
