using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Edge.Data.Objects;
using Edge.Core.Utilities;
using System.Reflection;

namespace Edge.Data.Pipeline.Mapping
{
	public class MappingConfiguration
	{
		public List<string> Usings = new List<string>();
		public Dictionary<Type, MappingContainer> Objects = new Dictionary<Type, MappingContainer>();
		public Dictionary<string, Delegate> ExternalMethods = new Dictionary<string, Delegate>();

		private List<EvaluatorExpression> _evalExpressions = new List<EvaluatorExpression>();
		internal int NextEvalID = 0;
		internal Evaluator Eval = new Evaluator();

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

			return Load(doc.DocumentElement);
		}

		/// <summary>
		/// Loads mapping configurations from an XmlElement.
		/// </summary>
		public static MappingConfiguration Load(XmlElement mappingXml)
		{
			// Create the root configuration with the System namespace
			var config = new MappingConfiguration();

			foreach (XmlNode node in mappingXml.ChildNodes)
			{
				// Check for allowed XML elements
				if (!(node is XmlElement))
				{
					if (node.NodeType == XmlNodeType.Whitespace || node.NodeType == XmlNodeType.Comment)
						continue;
					else
						throw new MappingConfigurationException(String.Format("<Object}>: Node type {0} is not allowed here.", node.NodeType));
				}

				var element = (XmlElement)node;
				if (element.Name == "Object")
				{
					var objectMapping = new MappingContainer() { Root = config };
					string typeName = element.HasAttribute("Type") ? element.GetAttribute("Type") : null;
					if (typeName == null)
						throw new MappingConfigurationException("<Object>: 'Type' attribute is missing.");
					objectMapping.TargetType = config.ResolveType(typeName);
					if (objectMapping.TargetType == null)
						throw new MappingConfigurationException(String.Format("<Object>: Type '{0}' could not be found. Did you forget a <Using>? <Using> elements must be defined before any <Object> elements that use it.", typeName));

					DeserializeMappings(config, objectMapping, element);
					config.Objects.Add(objectMapping.TargetType, objectMapping);
				}
				else if (element.Name == "Using")
				{
					config.Usings.Add(element.GetAttribute("Namespace"));
				}
				else
					throw new MappingConfigurationException(String.Format("<{0}> is not allowed here.", element.Name));
			}

			// Compile the evaluator
			config.Eval.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().FullName);
			config.Eval.Compile();

			return config;
		}


		/// <summary>
		/// Parses parentXml and finds MapCommand and ReadCommand objects that should be added to the parent container.
		/// </summary>
		private static void DeserializeMappings(MappingConfiguration config, MappingContainer parent, XmlElement parentXml)
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

					if (element.HasAttribute("Regex"))
						read.RegexPattern = element.GetAttribute("Regex");

					// Register it as a read command
					parent.ReadCommands.Add(read);
					
					// Add the command to the parent's inherited list, so that child map commands inherit it also
					parent.InheritedReads[read.Name] = read;
				}
				else if (element.Name == "Map")
				{
					// Handle mappings
					if (!element.HasAttribute("To"))
						throw new MappingConfigurationException("<Map>: Missing 'To' attribute.");

					MapCommand map = MapCommand.New(parent, element.GetAttribute("To"), returnInnermost: true);

					// Handle implicit read sources
					ReadCommand implicitRead = null;
					if (element.HasAttribute("Field"))
					{
						implicitRead = new ReadCommand()
						{
							Name = element.GetAttribute("Field"),
							Field = element.GetAttribute("Field"),
							IsImplicit = true
						};

						if (element.HasAttribute("Regex"))
							implicitRead.RegexPattern = element.GetAttribute("Regex");

						map.ReadCommands.Add(implicitRead); //map.TargetMemberType);
					}
					
					// Force parent to re-inherit, and then inherit from it
					map.Inherit();

					// Handle value expressions
					if (element.HasAttribute("Value"))
					{
						map.Value = new ValueFormat(map, element.GetAttribute("Value"));
					}
					else
					{
						if (implicitRead != null)
							map.Value = new ValueFormat(map, "{" + implicitRead.Field + "}");
					}

					// Recursively add child nodes
					DeserializeMappings(config, map, element);

					// Add to the parent
					parent.MapCommands.Add(map);
				}
				else
				{
					throw new MappingConfigurationException(String.Format("<Object>: Element '{0}' is not allowed here.", node.Name));
				}

			}
		}

		public Type ResolveType(string typeName)
		{
			Type t = Type.GetType(typeName, false);
			if (t != null)
				return t;

			// Search the namespaces for this type
			foreach (string us in this.Usings)
			{
				t = Type.GetType(String.Format(us, typeName), false);
				if (t != null)
					return t;
			}

			return null;
		}


	}
}
