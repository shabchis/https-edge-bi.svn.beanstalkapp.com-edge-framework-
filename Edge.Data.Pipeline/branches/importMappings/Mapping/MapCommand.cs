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
		/// The parent map command.
		/// </summary>
		public MappingContainer Parent { get; internal set; }

		/// <summary>
		/// The property or field (from reflection) that we are mapping to. (The "To" attribute.)
		/// </summary>
		public MemberInfo TargetMember { get; private set; }

		/// <summary>
		/// The key to use if TargetMemberType is IDictionary. (The "[]" part of the "To" attribute.)
		/// </summary>
		public object CollectionKey { get; private set; }

		/// <summary>
		/// The type of the value to apply (the "::" part of the "To" attribute").
		/// </summary>
		public Type ValueType { get; private set; }

		/// <summary>
		/// The expression that formats the value that is applied to the target member. (The "Value" attribute.)
		/// </summary>
		public ValueExpression Value { get; set; }

		// Fun with regex (http://xkcd.com/208/)
		private static Regex _levelRegex = new Regex(@"((?<member>[a-z_][a-z0-9_]*)(\[(?<indexer>.*)\])*(?<valueType>::[a-z_][a-z0-9_]*)*)",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

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
		internal static MapCommand AddToContainer(MappingContainer container, string targetExpression, bool returnInnermost = false)
		{
			if (container == null)
				throw new ArgumentNullException("container");

			var map = new MapCommand()
			{
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
					throw new MappingConfigurationException(String.Format("'{0}' is not a valid target for a map command.", targetExpression));

				// Do some error checking on target member name type
				string targetMemberName = memberGroup.Value;
				MemberInfo[] possibleMembers = map.TargetType.GetMember(targetMemberName, BindingFlags.Instance | BindingFlags.Public);
				if (possibleMembers == null || possibleMembers.Length < 1)
					throw new MappingConfigurationException(String.Format("The member '{0}' could not be found on {1}. Make sure it is public and non-static.", targetMemberName, targetType));
				if (possibleMembers.Length > 1)
					throw new MappingConfigurationException(String.Format("'{0}' matched more than one member in type {1}. Make sure it is a property or field.", targetMemberName, targetType));
				MemberInfo member = possibleMembers[0];
				if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
					throw new MappingConfigurationException(String.Format("'{0}' is not a property of field and cannot be mapped.", targetMemberName));
				if (member.MemberType == MemberTypes.Property && !((PropertyInfo)member).CanWrite)
					throw new MappingConfigurationException(String.Format("'{0}' is a read-only property and cannot be mapped.", targetMemberName));

				map.TargetMember = member;

				// ...................................
				// INDEXER

				// TODO: support more than one indexer (obj[9,2]) + support sequence of indexers (obj[9][2])

				Group indexerGroup = match.Groups["indexer"];
				if (indexerGroup.Success)
				{
					string indexer = indexerGroup.Value;
					Type indexerType = null;
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
						throw new MappingConfigurationException(String.Format("'{0}' does not support indexers.", targetMemberName));
					if (indexers.Length > 1)
						throw new MappingConfigurationException(String.Format("'{0}' has an index with more than one parameter - not currently supported.", targetMemberName));
					indexerType = indexers[0].ParameterType;

					// TODO: allow escaping the colon
					string[] lookup = indexer.Split(':');
					if (lookup.Length == 2)
					{
						// TODO: allow escaping the comma
						string[] parameters = lookup[1].Split(',');

						map.CollectionKey = new ValueLookup() { Name = lookup[0], Parameters = parameters, RequriedType = indexerType };
					}
					else if (lookup.Length == 1)
					{
						// No lookup required, convert the key from string
						TypeConverter converter = TypeDescriptor.GetConverter(indexerType);
						if (converter == null || !converter.IsValid(indexer))
							throw new MappingConfigurationException(String.Format("'{0}' cannot be converted to {1} for the {2} indexer.", indexer, indexerType, targetMemberName));

						map.CollectionKey = converter.ConvertFromString(indexer);
					}
					else
						throw new MappingConfigurationException(String.Format("'{0}' is not a valid indexer.", indexer));
				}

				// ...................................
				// VALUE TYPE

				Group valueTypeGroup = match.Groups["valueType"];
				if (valueTypeGroup.Success)
				{
					string valueTypeName = valueTypeGroup.Value;
					
					// Search the namespaces for this type
					foreach (string ns in map.Root.Namespaces)
					{
						Type t = Type.GetType(ns + "." + valueTypeName, false);
						if (t != null)
						{
							map.ValueType = t;
							break;
						}
					}

					if (map.ValueType == null)
						throw new MappingConfigurationException(String.Format("The type '{0}' cannot be found - are you missing a '<Using>'?", valueTypeName));
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
						Root = map.Root
					};

					map.MapCommands.Add(child);
					map = child;
				}

			}

			// ...................................
			return returnInnermost ? map : outermost;

		}




		public ReadCommand GetSource(string name, bool useParentSources = true)
		{
			throw new NotImplementedException();
		}

		public void Apply(object targetObject, Func<string, string> readFunction)
		{
			var readSources = new Dictionary<string, string>();
			this.Apply(targetObject, readFunction, readSources);
		}

		private void Apply(object targetObject, Func<string, string> readFunction, Dictionary<string, string> readSources)
		{
			//bool newSources = false;

			//// -------------------------------------------------------
			//// STEP 1: COLLECTIONS

			//// Determine if we're dealing with a collection
			//object currentCollection = null;
			//bool newCollection = false;
			//if (this.TargetMemberType.IsAssignableFrom(typeof(ICollection)))
			//{
			//    // Check if we need to create a new collection
			//    if (this.TargetMember is PropertyInfo)
			//        currentCollection = (this.TargetMember as PropertyInfo).GetValue(targetObject, null);
			//    else if (this.TargetMember is FieldInfo)
			//        currentCollection = (this.TargetMember as FieldInfo).GetValue(targetObject);

			//    // No collection found, create one
			//    if (currentCollection == null)
			//    {
			//        newCollection = true;
			//        try { currentCollection = Activator.CreateInstance(this.TargetMemberType); }
			//        catch (Exception ex)
			//        {
			//            throw new MappingException(string.Format(
			//                "Could not initialize the collection for {0}.{1}. See inner exception for more details.",
			//                    targetObject.GetType().Name,
			//                    this.TargetMember.Name
			//                ), ex);
			//        }
			//    }
			//}

			//// -------------------------------------------------------
			//// STEP 2: READ FROM SOURCE
			//foreach (ReadSource source in this.ReadSources)
			//{
			//    if (!newSources)
			//    {
			//        // Duplicate for sources for this branch only
			//        newSources = true;
			//        readSources = new Dictionary<string, string>(readSources);
			//    }

			//    if (String.IsNullOrWhiteSpace(source.Field))
			//        throw new MappingException("The 'Field' property must be defined.");

			//    // Validate the name
			//    string name;
			//    bool usingFieldName = false;
			//    if ( String.IsNullOrWhiteSpace(source.Name))
			//    {
			//        name = source.Field;
			//        usingFieldName = true;
			//    }
			//    else
			//        name = source.Name;

			//    if (!Regex.IsMatch(name, "[A-Za-z_][A-Za-z0-9_]*"))
			//    {
			//        throw new MappingException(String.Format(usingFieldName ?
			//            "The field name '{0}' cannot be used as the read source name because it includes illegal characters. Please specify a separate 'Name' attribute.":
			//            "The read source name '{0}' is not valid because it includes illegal characters.",
			//            name
			//        ));
			//    }

			//    string readValue = readFunction(source.Field);
			//    readSources[name] = readValue;

			//    // Capture groups
			//    if (source.Regex != null)
			//    {
			//        Match m = source.Regex.Match(readValue);
			//        foreach (string groupName in source.Regex.GetGroupNames())
			//        {
			//            readSources[name + "." + groupName] = m.Groups[groupName].Value;
			//        }


			//        //MatchCollection matches = source.Regex.Matches(source);
			//        //foreach (Match match in matches)
			//        //{
			//        //    if (!match.Success)
			//        //        continue;

			//        //    int fragmentCounter = 0;
			//        //    for (int g = 0; g < match.Groups.Count; g++)
			//        //    {
			//        //        Group group = match.Groups[g];
			//        //        string groupName = pattern.RawGroupNames[g];
			//        //        if (!group.Success || !AutoSegmentPattern.IsValidFragmentName(groupName))
			//        //            continue;

			//        //        // Save the fragment
			//        //        fragmentValues[pattern.Fragments[fragmentCounter++]] = group.Value;
			//        //    }
			//        //}
			//    }
			//}

			//// -------------------------------------------------------
			//// STEP 3: FORMAT VALUE

			//object mapValue;

			//// Get the required value, if necessary
			//if (Value != null)
			//{
			//    mapValue = Value;
			//}
			//else if (NewObjectType != null)
			//{
			//    // TODO-IF-EVER-THERE-IS-TIME-(YEAH-RIGHT): support constructor arguments

			//    try { mapValue = Activator.CreateInstance(this.NewObjectType); }
			//    catch(Exception ex)
			//    {
			//        throw new MappingException(string.Format(
			//            "Could not create new instance of {0} for applying to {1}.{2}. See inner exception for more details.",
			//            this.NewObjectType.FullName,
			//            targetObject.GetType().Name,
			//            this.TargetMember.Name
			//        ), ex);
			//    }
			//}

			//// -------------------------------------------------------
			//// STEP 4: APPLY VALUE

			//// Apply the value
			//if (currentCollection != null)
			//{
			//    object key = this.CollectionKeyLookup != null ?
			//        this.CollectionKeyLookup(this.CollectionKey) :
			//        this.CollectionKey;

			//    // Add the value to the collection
			//    if (currentCollection is IDictionary)
			//    {
			//        if (key == null)
			//            throw new MappingException(String.Format("Cannot use a null value as the key for the dictionary {0}.{1}.",
			//                    targetObject.GetType().Name,
			//                    this.TargetMember.Name,
			//                    this.CollectionKey
			//                ));

			//        var dict = (IDictionary)currentCollection;
			//        try { dict.Add(key, mapValue); }
			//        catch (Exception ex)
			//        {
			//            throw new MappingException(String.Format("Could not add the value to the dictionary {0}.{1}. See inner exception for more details.",
			//                targetObject.GetType().Name,
			//                this.TargetMember.Name
			//            ), ex);
			//        }
			//    }
			//    else if (currentCollection is IList)
			//    {
			//        var list = (IList)currentCollection;
			//        if (key != null)
			//        {
			//            if (key is Int32)
			//                list[(int)key] = mapValue;
			//            else
			//                throw new MappingException(String.Format("Cannot use the non-integer \"{2}\" as the index for the list {0}.{1}.",
			//                    targetObject.GetType().Name,
			//                    this.TargetMember.Name,
			//                    this.CollectionKey
			//                ));
			//        }
			//        else
			//            list.Add(mapValue);
			//    }
			//    else
			//    {
			//        throw new MappingException(String.Format("The collection {0}.{1} cannot be used as a mapping target because it does not implement either IList or IDictionary.",
			//            targetObject.GetType().Name,
			//            this.TargetMember.Name
			//        ));
			//    }

			//    // Apply the collection, if it is new
			//    if (newCollection)
			//    {
			//        try
			//        {
			//            if (this.TargetMember is PropertyInfo)
			//                (this.TargetMember as PropertyInfo).SetValue(targetObject, currentCollection, null);
			//            else if (this.TargetMember is FieldInfo)
			//                (this.TargetMember as FieldInfo).SetValue(targetObject, currentCollection);
			//        }
			//        catch (Exception ex)
			//        {
			//            throw new MappingException(string.Format(
			//                "Could not apply a collection to {0}.{1}. See inner exception for more details.",
			//                    targetObject.GetType().Name,
			//                    this.TargetMember.Name
			//                ), ex);
			//        }
			//    }

			//}
			//else
			//{
			//    // Apply the value directly to the member
			//    try
			//    {
			//        if (this.TargetMember is PropertyInfo)
			//            (this.TargetMember as PropertyInfo).SetValue(targetObject, value, null);
			//        else if (this.TargetMember is FieldInfo)
			//            (this.TargetMember as FieldInfo).SetValue(targetObject, value);
			//    }
			//    catch (Exception ex)
			//    {
			//        throw new MappingException(String.Format("Could not apply the value to the member {0}.{1}. See inner exception for more details.",
			//            targetObject.GetType().Name,
			//            this.TargetMember.Name
			//        ), ex);
			//    }
			//}

			//// -------------------------------------------------------
			//// STEP 5: RECURSION

			//// Activate child mappings on the value
			//if (value != null)
			//{
			//    foreach (Map spec in this.SubMaps)
			//    {
			//        // TODO: wrap this somehow for exception handling
			//        spec.Apply(value, readFunction, readSources);
			//    }
			//}
		}
	}



	public class ValueLookup
	{
		public string Name;
		public string[] Parameters;
		public Type RequriedType;
	}
}