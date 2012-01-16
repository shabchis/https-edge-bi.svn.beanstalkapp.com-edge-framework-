using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Mapping
{
	public class MappingConfiguration
	{
		public Dictionary<Type, ObjectMap> Objects = new Dictionary<Type, ObjectMap>();

		public Dictionary<string, Measure> Measures { get; set; }
		public Dictionary<Segment, SegmentValue> Segments { get; set; }

		/// <summary>
		/// Loads mapping configurations from a file.
		/// </summary>
		public static MappingConfiguration Load(string mappingFilePath)
		{
			// Load the XML to memory
			XmlDocument doc = new XmlDocument();
			try { doc.Load(mappingFilePath); }
			catch (Exception ex)
			{
				throw new MappingConfigurationException(String.Format("Failed to load mapping configuration file {0}. See inner exception for details.", mappingFilePath), ex);
			}

			//
			var output = new MappingConfiguration();
			foreach (XmlNode node in doc.DocumentElement.ChildNodes)
			{
				// Check for allowed XML elements
				if (!(node is XmlElement))
				{
					if (node.NodeType == XmlNodeType.Whitespace || node.NodeType == XmlNodeType.Comment)
						continue;
					else
						throw new MappingConfigurationException(String.Format("<{0}>: Node type {1} is not allowed here.", "Object", node.NodeType));
				}

				var objectXml = (XmlElement)node;
				var objectMapping = new ObjectMap();
				string typeName = objectXml.HasAttribute("Type") ? objectXml.GetAttribute("Type") : null;
				if (typeName == null)
					throw new MappingConfigurationException("<Object>: 'Type' attribute is missing.");
				objectMapping.TargetType = Type.GetType(typeName, false);
				if (objectMapping.TargetType == null)
					throw new MappingConfigurationException(String.Format("<Object>: Type '{0}' could not be found.", typeName));

				DeserializeMappings(objectMapping, objectXml);
			}
			return output;
		}


		/// <summary>
		/// Parses parentXml and finds MapCommand and ReadCommand objects that should be added to the parent container.
		/// </summary>
		private static void DeserializeMappings(MappingContainer parent, XmlElement parentXml)
		{
			foreach (XmlNode node in parentXml.ChildNodes)
			{
				// Check for allowed XML elements
				if (!(node is XmlElement))
				{
					if (node.NodeType == XmlNodeType.Whitespace || node.NodeType == XmlNodeType.Comment)
						continue;
					else
						throw new MappingConfigurationException(String.Format("<{0}>: Node type {1} is not allowed here.", parentXml.Name, node.NodeType));
				}

				var element = (XmlElement)node;
				if (element.Name == "Read")
				{
					// Handle explicit read sources
					var read = new ReadCommand();
					if (!element.HasAttribute("Field"))
						throw new MappingConfigurationException("<Read>: Missing 'Field' attribute.");
					read.Field = element.GetAttribute("Field");

					if (element.HasAttribute("Name"))
						read.Name = element.GetAttribute("Name");
					else
						read.Name = read.Field;

					// TODO: re-use the code from AutoSegments for resolving regex escape characters etc.
					//if (element.HasAttribute("Regex"))
					//	read.Name = new Regex(element.GetAttribute("Regex"));

					parent.ReadCommands.Add(read);

				}
				else if (element.Name == "Map")
				{
					// Handle mappings
					if (!element.HasAttribute("To"))
						throw new MappingConfigurationException("<Map>: Missing 'To' attribute.");

					var map = new MapCommand(parent, element.GetAttribute("To"));

					// Handle implicit read sources
					ReadCommand implicitRead = null;
					if (element.HasAttribute("Field"))
					{
						implicitRead = new ReadCommand();
						implicitRead.Field = element.GetAttribute("Field");

						// TODO: regex code
						//if (element.HasAttribute("Regex"))

						parent.ReadCommands.Add(implicitRead); //map.TargetMemberType);
					}

					// Handle value expressions
					if (element.HasAttribute("Value"))
					{
						map.Value = new ValueExpression(map, element.GetAttribute("Value"));
					}
					else
					{
						if (implicitRead == null)
							throw new MappingConfigurationException("<Map>: Missing either 'Field' or 'Value' attributes.");
						else
							map.Value = new ValueExpression(map, "{" + implicitRead.Field + "}");
					}

					// Recursively add child nodes
					DeserializeMappings(map, element);

					// Add to the parent
					parent.MapCommands.Add(map);
				}
				else
				{
					throw new MappingConfigurationException(String.Format("<Object>: Element '{0}' is not allowed here.", node.Name));
				}

			}
		}

	}
}
