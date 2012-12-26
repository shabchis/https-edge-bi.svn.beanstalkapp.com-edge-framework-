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
		/// An expression that is evaluated to determine whether the mapping operation should be performed.
		/// </summary>
		public EvalComponent Condition { get; set; }

		/// <summary>
		/// Indicates whether this map command is implicit (formed by a breakdown of the "To" expression).
		/// </summary>
		public bool IsImplicit { get; private set; }

		/// <summary>
		/// Indicates whether this command is optional, i.e. will not throw an exception if it fails.
		/// </summary>
		public bool IsRequired { get; internal set; }

		// Fun with regex (http://xkcd.com/208/)
		private static Regex _levelRegex = new Regex(@"((?<member>[a-z_][a-z0-9_]*)(\[(?<indexer>.*)\])?(::(?<valueType>[a-z_][a-z0-9_]*))*)",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		// Used to indicate that a target such as Creatives[] means add to collection if possible
		private static object ListAddingMode = new object();

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
		internal static MapCommand CreateChild(MappingContainer container, string targetExpression, XmlReader xml, ReadCommand implicitRead, bool returnInnermost = false)
		{
			if (container == null)
				throw new ArgumentNullException("container");

			var map = new MapCommand()
			{
				Parent = container,
				TargetType = container is MapCommand ? ((MapCommand)container).ValueType : container.TargetType,
				Root = container.Root
			};
			
			// Add to the parent
			container.MapCommands.Add(map);

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
				// READ COMMANDS

				if (implicitRead != null)
					map.ReadCommands.Add(implicitRead);
				map.Inherit();

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
					map.ValueType = itemProp.PropertyType;

					if (indexer.StartsWith("{") && indexer.EndsWith("}"))
					{
						// This is an eval indexer
						map.Indexer = new EvalComponent(map, indexer.Substring(1, indexer.Length-2), xml, inheritedReadOnly: true);
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

						map.Indexer = MapCommand.ListAddingMode;
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
					// Use the target member's value type only if no value type was found before (in the case of an indexer, for example)
					if (map.ValueType == null)
					{
						map.ValueType = member.MemberType == MemberTypes.Property ?
							((PropertyInfo)member).PropertyType :
							((FieldInfo)member).FieldType;
					}
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
            // .......................................
            // Process inherited read commands only
            foreach (ReadCommand read in this.InheritedReads.Values)
            {
                if (this.ReadCommands.Contains(read))
                    continue;

                read.Read(context);
            }

            // .......................................
            // Check condition - only external read commands are available here

			if (this.Condition != null)
			{
				var condition = (bool)this.Condition.GetOuput(context, inheritedOnly: true);
				if (!condition)
					return;
			}

			// .......................................
			// Process inner read commands only
			foreach (ReadCommand read in this.ReadCommands)
				read.Read(context);

			// .......................................
			// Apply mapping operation

			object nextTarget = target;

			// Check condition
			

			if (this.TargetMember != null)
			{
				PropertyInfo property = this.TargetMember is PropertyInfo ? (PropertyInfo)this.TargetMember : null;
				FieldInfo field = this.TargetMember is FieldInfo ? (FieldInfo)this.TargetMember : null;

				// .......................................
				// Get the command output

				object output = null;
				if (this.Value != null)
				{
					try { output = this.Value.GetOutput(context); }
					catch (Exception ex)
					{
						if (this.IsRequired)
							throw new MappingException(String.Format("Failed to get the output of the map command for {0}.{1}. See inner exception for details.", this.TargetType.Name, this.TargetMember.Name), ex);
						else
						{
							Log.Write(this.ToString(), String.Format("Failed to get the output of the map command for {0}.{1}.", this.TargetType.Name, this.TargetMember.Name), ex);
							return;
						}
					}
				}

				// .......................................
				// Get final value

				object value = null;
				if (output != null && !this.ValueType.IsAssignableFrom(output.GetType()))
				{

					// Try a converter when incompatible types are detected
					TypeConverter converter = TypeDescriptor.GetConverter(this.ValueType);
					if (converter != null && converter.CanConvertFrom(output.GetType()))
					{
						try { value = converter.ConvertFrom(output); }
						catch (Exception ex)
						{
							throw new MappingException(String.Format("Error while converting, '{0}' to {1} for applying to {2}.{3}, using TypeConverter.", output, this.ValueType, this.TargetType.Name, this.TargetMember.Name), ex);
						}
					}
					else // no type converter available
					{
						// Make C# 4.0 do all the implicit/explicit conversion work at runtime
						// social.msdn.microsoft.com/Forums/en-US/csharplanguage/thread/fe14d396-bc35-4f98-851d-ce3c8663cd79/

						MethodInfo dynamicCastMethod = CastGenericMethod.MakeGenericMethod(this.ValueType);
						try { value = dynamicCastMethod.Invoke(null, new object[] { output }); }
						catch (Exception ex)
						{
							throw new MappingException(String.Format("Error while converting, '{0}' to {1} for applying to {2}.{3}. using dynamic cast.", output, this.ValueType, this.TargetType.Name, this.TargetMember.Name), ex);
						}
					}
				}
				else
				{
					// Try to create a new 
					if (output == null && this.Value == null)
					{
						// Try to create an instance of the value type, since nothing was specified
						try { output = Activator.CreateInstance(this.ValueType); }
						catch (Exception ex)
						{
							throw new MappingException(String.Format("Cannot create a new instance of {0} for applying to {2}.{3}. (Did you forget the 'Value' attribute?)", this.ValueType, this.TargetType.Name, this.TargetMember.Name), ex);
						}
					}

					// Types might be compatible, just try to assign it
					value = output;
				}

				// .......................................
				// Check for indexers

				if (Object.Equals(this.Indexer, MapCommand.ListAddingMode))
				{
					IList list;
					try
					{
						// Try to get the list
						list = property != null ?
							(IList)property.GetValue(target, null) :
							(IList)field.GetValue(target);
					}
					catch (InvalidCastException ex)
					{
						throw new MappingException(String.Format("{0}.{1} is not an IList and cannot be set with the \"[]\" notation.", this.TargetType.Name, this.TargetMember.Name), ex);
					}
					catch (Exception ex)
					{
						throw new MappingException(String.Format("Error while mapping to {0}.{1}. See inner exception for details.", this.TargetType.Name, this.TargetMember.Name), ex);
					}

					if (list == null)
					{
						// List is empty, create a new list
						Type listType = property != null ? property.PropertyType : field.FieldType;
						try { list = (IList)Activator.CreateInstance(listType); }
						catch (Exception ex)
						{
							throw new MappingException(String.Format("Cannot create a new list of type {0} for applying to {2}.{3}. To avoid this error, make sure the list is not null before applying the mapping.", listType, this.TargetType.Name, this.TargetMember.Name), ex);
						}
					}

					if (property != null)
						property.SetValue(target, list, null);
					else
						field.SetValue(target, list);

					// Add to end of list
					list.Add(value);
				}
				else if (this.IndexerType != null)
				{
					// Actual indexer
					object indexer = null;
					if (this.Indexer is EvalComponent)
					{
						indexer = ((EvalComponent)this.Indexer).GetOuput(context, inheritedOnly: true);
					}
					else
					{
						indexer = this.Indexer;
					}

					Type collectionType = property != null ? property.PropertyType : field.FieldType;
					object collection;
					
					// Try to get the object with the indexer
					collection = property != null ?
						property.GetValue(target, null) :
						field.GetValue(target);

					if (collection == null)
					{
						try { collection = Activator.CreateInstance(collectionType); }
						catch (Exception ex)
						{
							throw new MappingException(String.Format("Cannot create a new object of type {0} for applying to {2}.{3}. To avoid this error, make sure the property is not null before applying the mapping.", collectionType, this.TargetType.Name, this.TargetMember.Name), ex);
						}

						if (property != null)
							property.SetValue(target, collection, null);
						else
							field.SetValue(target, collection);
					}

					PropertyInfo itemProp = collectionType.GetProperty("Item");
					itemProp.SetValue(collection, value, new object[]{indexer});
				}
				else
				{
					// .......................................
					// Apply to target member

					try
					{
						if (property != null)
						{
							// Apply to the property
							property.SetValue(target, value, null);

						}
						else if (field != null)
						{
							// Apply to the field
							field.SetValue(target, value);
						}
					}
					catch (Exception ex)
					{
						throw new MappingException(String.Format("Failed to map '{0}' to {1}.{2}. See inner exception for details.", value, this.TargetType.Name, this.TargetMember.Name), ex);
					}
				}

				nextTarget = value;
			}


			// .......................................
			// Trickle down

			base.OnApply(nextTarget, context);
		}


		public static T Cast<T>(dynamic o)
		{
			return (T)o;
		}
		static MethodInfo CastGenericMethod = typeof(MapCommand).GetMethod("Cast");
	}
}