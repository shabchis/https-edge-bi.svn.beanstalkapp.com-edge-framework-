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
using Eggplant.Model;

namespace Eggplant.Persistence.Providers.Xml
{
	public class XmlProvider : PersistenceProvider
	{
		class Namespaces
		{
			public const string XmlProviderMappings = @"http://schemas.eggplant-framework.org/1.0/providers/xml/mappings";
		}

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

			
		}

		private ObjectMapping ReadObjectMapping(XmlReader reader)
		{
			
			if (reader.NodeType != XmlNodeType.Element && reader.Name != "xs:complexType")
				throw new InvalidOperationException("Object mapping can only be read from a xs:complexType element.");

			int depth = reader.Depth;
			ObjectMapping mapping = new ObjectMapping();
			string mapsTo = null;

			// Find mapping info
			while (reader.MoveToNextAttribute())
			{
				if (reader.NamespaceURI != Namespaces.XmlProviderMappings)
					continue;

				if (reader.Name == "mapsTo" )
				{
					mapsTo = reader.Value;
				}
				else if (reader.Name == "queryMapping")
				{
					// TODO: parse query mapping
				}
			}
			if (mapsTo == null)
				return null;
			else
				mapping.TargetObject = this.Model.Definitions[mapsTo];

			while (reader.Read())
			{
				// Reached the end of this object mapping
				if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
					break;

				if (reader.NodeType == XmlNodeType.Element)
				{
					var propMapping = new PropertyMapping();
					propMapping.NodeName = reader.GetAttribute("name");
					string propertyName = reader.GetAttribute("mapsTo", Namespaces.XmlProviderMappings) ?? propMapping.NodeName;
					PropertyDefinition propDef = mapping.TargetObject.Properties[propertyName];
					propMapping.Property = propDef;

					if (reader.LocalName == "attribute")
					{
					}
					else if (reader.LocalName == "element")
					{
						//TODO: if (propDef.IsList // check if list
					}
				}
			}

			return mapping;
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
