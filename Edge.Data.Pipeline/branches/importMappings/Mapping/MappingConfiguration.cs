using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Edge.Data.Objects;
using Edge.Core.Utilities;
using System.Reflection;
using System.Configuration;
using Edge.Core.Configuration;
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

		public MappingConfiguration Load()
		{
			var mapping = new MappingConfiguration();
			using (XmlTextReader reader = new XmlTextReader(new StringReader(this.RawXml)))
			{
				string path = reader.GetAttribute("Path");
				if (!string.IsNullOrEmpty(path))
					mapping.Load(path);
				else
					mapping.Load(reader);
			}

			return mapping;
		}
	}

	public class MappingConfiguration
	{
		
		public string SourcePath { get; private set; }

		public List<string> Usings { get; set; }
		public Dictionary<Type, MappingContainer> Objects { get; set; }
		public Dictionary<string, Delegate> ExternalMethods { get; set; }
		public Func<string, object> OnFieldRequired {get; set;}

		private List<EvaluatorExpression> _evalExpressions = new List<EvaluatorExpression>();
		internal int NextEvalID = 0;
		internal Evaluator Eval = new Evaluator()
		{
			Expressions = new List<EvaluatorExpression>(),
			ExternalFunctions = new Dictionary<string,Delegate>(),
			ReferencedAssemblies = new List<string>(),
			UsingNamespaces = new List<string>()
		};


		public MappingConfiguration()
		{
			this.Usings = new List<string>();
			this.Objects = new Dictionary<Type, MappingContainer>();
			this.ExternalMethods = new Dictionary<string, Delegate>();
		}


		/// <summary>
		/// Loads mapping configurations from a file.
		/// </summary>
		public void Load(string mappingFilePath)
		{
			XmlTextReader reader; 
			try { reader = new XmlTextReader(mappingFilePath); }
			catch (Exception ex)
			{
				throw new MappingConfigurationException(String.Format("Failed to load mapping configuration file {0}. See inner exception for details.", mappingFilePath), ex);
			}
			using (reader)
			{
				this.SourcePath = mappingFilePath;
				this.Load(reader);
			}
		}

		/// <summary>
		/// Loads mapping configurations from an XML reader.
		/// </summary>
		public void Load(XmlReader mappingXml)
		{
			while (mappingXml.Read())
			{
				// Check for allowed XML nodex
				if (!(mappingXml.NodeType == XmlNodeType.Element))
				{
					if (mappingXml.NodeType == XmlNodeType.Text)
						throw new MappingConfigurationException(String.Format("Node type {0} is not allowed here.", mappingXml.NodeType), "Object", mappingXml);
					else
						continue;
				}
				else if (mappingXml.Name == "MappingConfiguration")
				{
					// Read into content
					continue;
				}
				if (mappingXml.Name == "Object")
				{
					var objectMapping = new MappingContainer() { Root = this };
					string typeName = mappingXml.GetAttribute("Type");
					if (typeName == null)
						throw new MappingConfigurationException("'Type' attribute is missing.", "Object", mappingXml);
					objectMapping.TargetType = this.ResolveType(typeName);
					if (objectMapping.TargetType == null)
						throw new MappingConfigurationException(String.Format("Type '{0}' could not be found. Did you forget a <Using>? <Using> elements must be defined before any <Object> elements that use it.", typeName), "Object", mappingXml);

					if (!mappingXml.IsEmptyElement)
						DeserializeMappings(this, objectMapping, mappingXml);

					this.Objects.Add(objectMapping.TargetType, objectMapping);
				}
				else if (mappingXml.Name == "Using")
				{
					this.Usings.Add(mappingXml.GetAttribute("Namespace"));
				}
				else
					throw new MappingConfigurationException(String.Format("<{0}> is not allowed here.", mappingXml.Name), mappingXml);
			}

			// Add external methods to the evaluator
			foreach (var external in this.ExternalMethods)
				this.Eval.ExternalFunctions.Add(external.Key, external.Value);

			// Compile the evaluator
			this.Eval.ReferencedAssemblies.Add("Edge.Core.dll");
			this.Eval.ReferencedAssemblies.Add("Edge.Data.Pipeline.dll");
			this.Eval.Compile();

		}

		/// <summary>
		/// Combines the current configuration with the specified configuration, overriding any conflicting mappings.
		/// </summary>
		/// <param name="otherConfig">Mappings with which to extend the current configuration.</param>
		public void Extend(MappingConfiguration otherConfig)
		{
			foreach (MappingContainer objectMappings in otherConfig.Objects.Values)
			{
			}
		}

		private void ExtendContainer()
		{
		}

		/// <summary>
		/// Parses parentXml and finds MapCommand and ReadCommand objects that should be added to the parent container.
		/// </summary>
		private static void DeserializeMappings(MappingConfiguration config, MappingContainer parent, XmlReader parentXml)
		{
			int currentDepth = parentXml.Depth;
			string currentName = parentXml.Name;

			while (parentXml.Read())
			{
				// This is the exit condition
				if (parentXml.NodeType == XmlNodeType.EndElement && parentXml.Name == currentName && parentXml.Depth == currentDepth)
				{
					break;
				}
				else if (parentXml.NodeType == XmlNodeType.Text)
				{
					throw new MappingConfigurationException(String.Format("<{0}> is not allowed here.", parentXml.Name), parentXml);
				}
				else if (parentXml.NodeType == XmlNodeType.Element)
				{
					// Just an alias to make a distinction
					XmlReader element = parentXml;

					if (element.Name == "Read")
					{
						// Handle explicit read sources
						var read = new ReadCommand();
						read.Field = element.GetAttribute("Field");
						if (read.Field == null)
							throw new MappingConfigurationException("Missing 'Field' attribute.", "Read", element);

						try
						{
							string name = element.GetAttribute("Name");
							read.Name = name != null ? name : read.Field;
						}
						catch(MappingConfigurationException ex)
						{
							if (read.Name != null)
								throw new MappingConfigurationException(ex.Message, "Read", parentXml, ex);
							else
								throw new MappingConfigurationException(String.Format("'{0}' cannot be used as the command name. Please specify a 'Name' attribute.", read.Field), "Read", parentXml, ex);
						}

						read.RegexPattern = element.GetAttribute("Regex");

						// Register it as a read command
						parent.ReadCommands.Add(read);

						// Add the command to the parent's inherited list, so that child map commands inherit it also
						parent.InheritedReads[read.Name] = read;
					}
					else if (element.Name == "Map")
					{
						// Handle mappings
						string to = element.GetAttribute("To");
						if (to == null)
							throw new MappingConfigurationException("Missing 'To' attribute.", "Map", element);

						MapCommand map = MapCommand.CreateChild(parent, to, element, returnInnermost: true);

						// Handle implicit read sources
						ReadCommand implicitRead = null;
						string implicitFieldName = element.GetAttribute("Field");
						if (implicitFieldName != null)
						{
							
							if (!ReadCommand.IsValidVarName(implicitFieldName))
								throw new MappingConfigurationException(String.Format(
									"'{0}' cannot be used as an implicit read command name because it is not a valid C# variable name. You must define a separate <Read> command with an explicit name.",
									implicitFieldName),
									"Map",
									element);

							try
							{
								implicitRead = new ReadCommand()
								{
									Name = implicitFieldName,
									Field = implicitFieldName,
									RegexPattern = element.GetAttribute("Regex"),
									IsImplicit = true
								};
							}
							catch (MappingConfigurationException ex)
							{
								throw new MappingConfigurationException(ex.Message, "Map", element, ex);
							}
							

							map.ReadCommands.Add(implicitRead); //map.TargetMemberType);
						}

						// Force parent to re-inherit, and then inherit from it
						map.Inherit();

						// Handle value expressions
						string valueFormat =  element.GetAttribute("Value");
						try
						{
							if (valueFormat != null)
							{
								map.Value = new ValueFormat(map, valueFormat, element);
							}
							else
							{
								if (implicitRead != null)
									map.Value = new ValueFormat(map, "{" + implicitRead.Name + "}", element);
							}
						}
						catch (MappingConfigurationException ex)
						{
							throw new MappingConfigurationException(ex.Message, "Map", element, ex);
						}

						// Recursively add child nodes
						if (!element.IsEmptyElement)
							DeserializeMappings(config, map, element);
					}
					else
					{
						throw new MappingConfigurationException(String.Format("Element '{0}' is not allowed here.", parentXml.Name), parentXml);
					}
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
