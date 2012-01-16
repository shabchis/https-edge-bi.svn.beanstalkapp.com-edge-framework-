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
	public class MapCommand: MappingContainer
	{
		/// <summary>
		/// The parent mapping item.
		/// </summary>
		public MappingContainer Parent { get; set; }

		/// <summary>
		/// The property or field (from reflection) that we are mapping to. (The "To" attribute.)
		/// </summary>
		public MemberInfo TargetMember { get; set; }

		/// <summary>
		/// The type of the target member ("To" attribute). Equals to TargetMember.MemberType.
		/// </summary>
		public Type TargetMemberType { get; private set; }

		/// <summary>
		/// The key to use if TargetMemberType is IDictionary. (The "[]" part of the "To" attribute.)
		/// </summary>
		public object CollectionKey { get; set; }

		/// <summary>
		/// The new object to create (the "::" part of the "To" attribute"). Default is null, which means use Target.
		/// </summary>
		public Type NewObjectType { get; set; }

		/// <summary>
		/// The expression that formats the value that is applied to the target member. (The "Value" attribute.)
		/// </summary>
		public ValueExpression Value { get; set; }


		public MapCommand(Type parentType, string targetExpression)
		{
			while (!string.IsNullOrEmpty(targetExpression))
			{
				string currentObjectName = GetCurrentObjectName(ref targetExpression);
				if (TargetMember == null)
				{
					this.TargetMember = parentType.GetMember(currentObjectName)[0];
					this.TargetMemberType = TargetMember.GetType();
					if (TargetMemberType.IsAssignableFrom(typeof(ICollection)))
					{
						currentObjectName = GetCurrentObjectName(ref targetExpression);
						if (currentObjectName.StartsWith("[") && currentObjectName.EndsWith("]"))
						{
							//this is another confirmation that it's collection
							currentObjectName=currentObjectName.Remove(0,1);
							currentObjectName=currentObjectName.Remove(currentObjectName.Length-1,1);
							string[] typeAndName = currentObjectName.Split(':');
							//CollectionKey=
						}

					}
				}
				else
				{
					
				}
			}
		}

		private string GetCurrentObjectName(ref string propertyName)
		{
			
			StringBuilder currentPropertyName = new StringBuilder();			
			char[] propertyNameChar = propertyName.ToCharArray();
			bool bExit = false;
			foreach (var item in propertyName.ToCharArray())
			{
				switch (item)
				{
					case '[':
						{
							if (currentPropertyName.Length > 0)
								bExit = true;
							else
							{
								currentPropertyName.Append(item);
								propertyName.Remove(0, 1);
							}
							break;
						}
					case ']':
						{
							propertyName.Remove(0, 1);
							currentPropertyName.Append(item);
							bExit = true;
							break;
							
						}
					default:
						{
							propertyName.Remove(0, 1);
							currentPropertyName.Append(item);
							break;
						}
				}
				if (bExit)
					break;
				
			}
			return currentPropertyName.ToString();
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

	




	


	
}