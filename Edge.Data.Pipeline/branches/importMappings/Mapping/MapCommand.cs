using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using Edge.Data.Objects;
using Edge.Data.Pipeline;
using System.Configuration;
using Edge.Core.Configuration;
using System.Xml;
using System.Collections;
using Edge.Core.Utilities;
using System.ComponentModel;

namespace Edge.Data.Pipeline.Mapping
{
	/// <summary>
	/// Applies a value to a property or field of an object.
	/// </summary>
	public class MapCommand : MappingContainer
	{
		/// <summary>
		/// The property or field (from reflection) that we are mapping to. (The "To" attribute.)
		/// </summary>
		public MemberInfo TargetMember { get; private set; }

		/// <summary>
		/// The value to use if TargetMemberType supports an indexer. (The "[]" part of the "To" attribute.)
		/// </summary>
		public object Indexer { get; private set; }

		/// <summary>
		/// Type type of the the Indexer.
		/// </summary>
		public Type IndexerType { get; private set; }

		/// <summary>
		/// The type of the value to apply (the "::" part of the "To" attribute").
		/// </summary>
		public Type ValueType { get; private set; }

		/// <summary>
		/// The expression that formats the value that is applied to the target member. (The "Value" attribute.)
		/// </summary>
		public ValueFormat Value { get; set; }

		/// <summary>
		/// Indicates whether this map command is implicit (formed by a breakdown of the "To" expression).
		/// </summary>
		public bool IsImplicit { get; private set; }

		// Fun with regex (http://xkcd.com/208/)
		private static Regex _levelRegex = new Regex(@"((?<member>[a-z_][a-z0-9_]*)(\[(?<indexer>.*)\])?::((?<valueType>[a-z_][a-z0-9_]*))*)",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		// Used to indicate that a target such as Creatives[] means add to collection if possible
		private static object EmptyIndexer = new object();

		/// <summary>
		/// Constructor is private.
		/// </summary>
		private MapCommand()
		{
		}

		/// <summary>
		/// Creates a new map command for a target type, parsing the supplied expression.
		/// </summary>
		/// <param name="returnInnermost">If true, returns the last nested map created if the expression has multiple parts. If false, returns the top level map.</param>
		internal static MapCommand New(MappingContainer container, string targetExpression, XmlReader xml, bool returnInnermost = false)
		{
			if (container == null)
				throw new ArgumentNullException("container");

			var map = new MapCommand()
			{
				Parent = container,
				TargetType = container.TargetType,
				Root = container.Root
			};

			// Keep track of the first one created
			var outermost = map;

			// No target expression - this is okay, so just return it
			if (String.IsNullOrEmpty(targetExpression))
				return map;

			// Parse the expression
			MatchCollection matches = _levelRegex.Matches(targetExpression);

			for (int i = 0; i < matches.Count; i++)
			{
				Match match = matches[i];

				// ...................................
				// MEMBER

				Group memberGroup = match.Groups["member"];
				if (!memberGroup.Success)
					throw new MappingConfigurationException(String.Format("'{0}' is not a valid target for a map command.", targetExpression), "Map", xml);

				// Do some error checking on target member name type
				string targetMemberName = memberGroup.Value;
				MemberInfo[] possibleMembers = map.TargetType.GetMember(targetMemberName, BindingFlags.Instance | BindingFlags.Public);
				if (possibleMembers == null || possibleMembers.Length < 1)
					throw new MappingConfigurationException(String.Format("The member '{0}' could not be found on {1}. Make sure it is public and non-static.", targetMemberName, map.TargetType), "Map", xml);
				if (possibleMembers.Length > 1)
					throw new MappingConfigurationException(String.Format("'{0}' matched more than one member in type {1}. Make sure it is a property or field.", targetMemberName, map.TargetType));
				MemberInfo member = possibleMembers[0];
				if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
					throw new MappingConfigurationException(String.Format("'{0}' is not a property or field and cannot be mapped.", targetMemberName), "Map", xml);
				if (member.MemberType == MemberTypes.Property && !((PropertyInfo)member).CanWrite)
					throw new MappingConfigurationException(String.Format("'{0}' is a read-only property and cannot be mapped.", targetMemberName), "Map", xml);

				map.TargetMember = member;

				// ...................................
				// INDEXER

				// TODO: support more than one indexer (obj[9,2]) + support sequence of indexers (obj[9][2])

				Group indexerGroup = match.Groups["indexer"];
				if (indexerGroup.Success)
				{
					string indexer = indexerGroup.Value.Trim();
					Type memberType = null;

					// Determine indexer type
					if (member.MemberType == MemberTypes.Property)
						memberType = ((PropertyInfo)member).PropertyType;
					else
						memberType = ((FieldInfo)member).FieldType;

					PropertyInfo itemProp = memberType.GetProperty("Item");
					ParameterInfo[] indexers = null;
					if (itemProp != null)
						indexers = itemProp.GetIndexParameters();
					if (indexers == null || indexers.Length == 0)
						throw new MappingConfigurationException(String.Format("'{0}' does not support indexers.", targetMemberName), "Map", xml);
					if (indexers.Length > 1)
						throw new MappingConfigurationException(String.Format("'{0}' has an index with more than one parameter - not currently supported.", targetMemberName), "Map", xml);
					map.IndexerType = indexers[0].ParameterType;

					if (indexer.StartsWith("{") && indexer.EndsWith("}"))
					{
						// This is an eval indexer
						map.Indexer = new EvalComponent(map, indexer.Substring(1, indexer.Length-2), xml);
					}
					else if (indexer.Length > 0)
					{
						// No eval required, convert the key from string
						TypeConverter converter = TypeDescriptor.GetConverter(map.IndexerType);
						if (converter == null || !converter.IsValid(indexer))
							throw new MappingConfigurationException(String.Format("'{0}' cannot be converted to {1} for the {2} indexer.", indexer, map.IndexerType, targetMemberName), "Map", xml);

						map.Indexer = converter.ConvertFromString(indexer);
					}
					else
					{
						// Empty indexers (i.e. 'Add' method) only valid with IList
						if (!typeof(IList).IsAssignableFrom(memberType))
							throw new MappingConfigurationException(String.Format("Invalid indexer defined for target '{0}'.", targetMemberName), "Map", xml);

						map.Indexer = MapCommand.EmptyIndexer;
					}
				}

				// ...................................
				// VALUE TYPE

				Group valueTypeGroup = match.Groups["valueType"];
				if (valueTypeGroup.Success)
				{
					string valueTypeName = valueTypeGroup.Value;

					map.ValueType = map.Root.ResolveType(valueTypeName);

					if (map.ValueType == null)
						throw new MappingConfigurationException(String.Format("The type '{0}' cannot be found - are you missing a '<Using>'?", valueTypeName), "Map", xml);
				}
				else
				{
					map.ValueType = member.MemberType == MemberTypes.Property ?
						((PropertyInfo)member).PropertyType :
						((FieldInfo)member).FieldType;
				}

				// ...................................
				// DRILL DOWN

				if (i < matches.Count - 1)
				{
					// Since there are more expression matches, create a child map and continue the loop
					var child = new MapCommand()
					{
						TargetType = map.ValueType,
						Parent = map,
						Root = map.Root,
						IsImplicit = true
					};

					map.MapCommands.Add(child);
					map = child;
				}

			}

			// ...................................

			return returnInnermost ? map : outermost;

		}


		protected override void OnApply(object target, MappingContext context)
		{
			// Read values as necessary
			foreach (ReadCommand read in this.InheritedReads.Values)
			{
				// Read from source if necessary
				object fieldValue;
				if (!context.ReadFields.TryGetValue(read.Field, out fieldValue))
				{
					if (this.Root.OnFieldRead == null)
						throw new MappingException("Cannot apply mappings because the OnFieldRead delegate is not set.");

					fieldValue = this.Root.OnFieldRead(read.Field);
					context.ReadFields.Add(read.Field, fieldValue);
				}
			}
		}
	}
}