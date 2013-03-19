using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using Eggplant.Entities.Queries;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence.SqlServer;

namespace Edge.Data.Objects
{
	public static class EdgeUtility
	{
		public static EntitySpace EntitySpace { get; private set; }

		static EdgeUtility()
		{
			EdgeUtility.EntitySpace = new EntitySpace();
		}

		public static readonly Func<object, object> ConvertAccountToID = ac => ac == null ? -1 : ((Account)ac).ID;
		public static readonly Func<object, object> ConvertChannelToID = ch => ch == null ? -1 : ((Channel)ch).ID;

		public static SqlPersistenceAction GetPersistenceAction(string fileName, string templateName)
		{
			const string tplSeparatorPattern = @"^--\s*#\s*TEMPLATE\s+(.*)$";
			Regex tplSeparatorRegex = new Regex(tplSeparatorPattern, RegexOptions.Singleline);

			var templateString = new StringBuilder();
			Assembly asm = Assembly.GetExecutingAssembly();
			using (StreamReader reader = new StreamReader(asm.GetManifestResourceStream(@"Edge.Data.Objects.Mappings." + fileName)))
			{
				bool readingTemplate = false;
				
				while (!reader.EndOfStream)
				{
					string line = reader.ReadLine();
					if (!readingTemplate)
					{
						Match m = tplSeparatorRegex.Match(line);
						if (m.Success && m.Groups[1].Value.Trim() == templateName)
							readingTemplate = true;
					}
					else
					{
						if (tplSeparatorRegex.IsMatch(line))
							break;
						else
							templateString.AppendLine(line);
					}

				}
			}

			if (templateString.Length == 0)
				throw new Exception("Template not found in resource.");

			return new SqlPersistenceAction(templateString.ToString(), System.Data.CommandType.Text);
		}

		#region ParseEdgeTemplate (disabled)
		// ===========================
		#if NOPE
		enum ParseState
		{
			BeforeColumns = 0,
			ColumnParseStarted = 1,
			ColumnParseEnded = 2
		}

		static Regex _columnRegex = new Regex(@"(?<columnSyntax>.*)\s+as\s+(?<columnName>[a-zA-Z_]\w*)?\s*($|,)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
		static Regex _columnsStartRegex = new Regex(@"(--\s*#\s*COLUMNS-START\s*$)|(/\*\s*#\s*COLUMNS-START\s*\*/)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
		static Regex _columnsEndRegex = new Regex(@"(--\s*#\s*COLUMNS-END\s*$)|(/\*\s*#\s*COLUMNS-END\s*\*/)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
		static Regex _filterRegex = new Regex(@"(--\s*#\s*FILTER\s*$)|(/\*\s*#\s*FILTER\s*\*/)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
		static Regex _sortingRegex = new Regex(@"(--\s*#\s*SORTING\s*$)|(/\*\s*#\s*SORTING\s*\*/)", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		public static SubqueryTemplate ParseEdgeTemplate(this SubqueryTemplate subqueryTemplate)
		{
			var state = ParseState.BeforeColumns;
			var textBuilder = new StringBuilder();
			var columnText = new StringBuilder();
			using (var reader = new StringReader(subqueryTemplate.CommandText))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					if (state == ParseState.BeforeColumns)
					{
						// Find COLUMNS-START
						Match colStartMatch = _columnsStartRegex.Match(line);
						if (colStartMatch.Success)
						{
							// Keep all text until the columsn start was found
							textBuilder.Append(line.Substring(0, colStartMatch.Index));

							int columnTextStartIndex = colStartMatch.Index + colStartMatch.Length;
							int columnTextEndIndex;
							
							Match colEndMatch = _columnsEndRegex.Match(line);
							if (colEndMatch.Success)
							{
								columnTextEndIndex = colEndMatch.Index;
								textBuilder.AppendLine(" {columns} ");
								textBuilder.AppendLine(line.Substring(columnTextEndIndex + colEndMatch.Length, line.Length - (columnTextEndIndex + colEndMatch.Length)));
								state = ParseState.ColumnParseEnded;
							}
							else
							{
								columnTextEndIndex = line.Length;
								state = ParseState.ColumnParseStarted;
							}

							columnText.AppendLine(line.Substring(columnTextStartIndex, columnTextEndIndex - columnTextStartIndex));
						}
						else
						{
							// Not found, append entire line
							textBuilder.AppendLine(line);
						}
					}
					else if (state == ParseState.ColumnParseStarted)
					{
						// Find COLUMNS-END
						Match colEndMatch = _columnsEndRegex.Match(line);
						if (colEndMatch.Success)
						{
							columnText.Append(line.Substring(0, colEndMatch.Index));
							textBuilder.AppendLine(" {columns} ");
							textBuilder.AppendLine(line.Substring(colEndMatch.Index + colEndMatch.Length, line.Length - (colEndMatch.Index + colEndMatch.Length)));
							state = ParseState.ColumnParseEnded;
						}
						else
							columnText.AppendLine(line);
					}
					else
					{
						line = _filterRegex.Replace(line, " {filter} ");
						line = _sortingRegex.Replace(line, " {sorting} ");
						textBuilder.AppendLine(line);
					}
				}

				subqueryTemplate.CommandText = textBuilder.ToString();

				// Parse columns and match them with already in-place conditions
				MatchCollection columnMatches = _columnRegex.Matches(columnText.ToString());
				foreach (Match columnMatch in columnMatches)
				{
					string columnName = columnMatch.Groups["columnName"].Value;
					SubqueryConditionalColumn columnCondition;
					if (subqueryTemplate.ConditionalColumns.TryGetValue(columnName, out columnCondition))
						columnCondition.ColumnSyntax = columnMatch.Groups["columnSyntax"].Value.Trim();
					else
						throw new EdgeTemplateException(String.Format("Column '{0}' in the command SQL must first be defined as a conditional column (subquery.ConditionalColumn()).", columnName));
				}
			}

			return subqueryTemplate;
		}
		#endif

		// ===========================
		#endregion

		/// <summary>
		/// Shortcut for mapping an EdgeObject reference from fields.
		/// </summary>
		public static void MapEdgeObject<T>(this Mapping<T> mapping, string fieldGK, string fieldTypeID, string fieldClrType)
			where T: EdgeObject
		{
			mapping
				//.Identity(EdgeObject.Identites.GK)
				.Do(context => context.NullIf<object>(fieldGK, gk => gk == null))
				.Set(context => (T)Activator.CreateInstance(Type.GetType(context.GetField<string>(fieldClrType))))
				.Map<EdgeType>(EdgeObject.Properties.EdgeType, edgeType => edgeType
					.Map<int>(EdgeType.Properties.TypeID, fieldTypeID)
				)
				.Map<long>(EdgeObject.Properties.GK, fieldGK)
			;
		}

		/*
		/// <summary>
		/// Shortcut for getting an EdgeObject reference from fields during runtime.
		/// </summary>
		public static T GetEdgeObject<T>(this MappingContext<T> context, string fieldGK, string fieldTypeID, string fieldClrType)
			where T : EdgeObject
		{
			if (context.GetField<object>(fieldGK) == null)
				return null;

			T obj = context.Cache.GetOrCreate(
				Type.GetType(context.GetField<string>(fieldClrType)),
				EdgeObject.Identites.Default,
				EdgeObject.Identites.Default.NewIdentity(context.GetField<long>(fieldGK))
			);

			if (obj.EdgeType == null)
				obj.EdgeType = context.Cache.GetOrCreate<EdgeType>(
					EdgeType.Identities.Default,
					EdgeType.Identities.Default.NewIdentity(context.GetField(fieldTypeID))
				);

			return obj;
		}
		*/

		/*
		public static Mapping<T> MapEdgeField<T, V>(this Mapping<T> mapping, EntityProperty<T, V> property)
			where T : EdgeObject
		{
			return mapping
				.Map<V>(property, prop => prop
					.Set(context =>
					{
						EdgeField field = context.Cache.Get<EdgeField>(EdgeField.Identities.Default, property.Name);
						return context.GetField<V>(String.Format("{0}_Field{1}", field.ColumnPrefix, field.ColumnIndex));
					})
				);
		}
		*/

		/// <summary>
		/// Shortcut for ensuring ID is not -1.
		/// </summary>
		public static void NullIf<V>(this MappingContext context, string idField, Func<V, bool> condition)
		{
			if (condition(context.GetField<V>(idField)))
			{
				context.Target = null;
				context.Break();
			}
		}

		/// <summary>
		/// Shortcut for mapping a dictionary from a subquery.
		/// </summary>
		public static Mapping<ParentT> MapDictionaryFromSubquery<ParentT, KeyT, ValueT>(
			this Mapping<ParentT> mapping,
			EntityProperty<ParentT, Dictionary<KeyT, ValueT>> dictionaryProperty,
			string subqueryName,
			Action<Mapping<ParentT>> parentMappingFunction,
			Action<Mapping<KeyT>> keyMappingFunction,
			Action<Mapping<ValueT>> valueMappingFunction
		)
		{

			mapping.Map<Dictionary<KeyT, ValueT>>(dictionaryProperty, dictionary => dictionary
				.Subquery(subqueryName, subquery => subquery
					.Map<ParentT>("parent", parentMappingFunction)
					.Map<KeyT>("key", keyMappingFunction)
					.Map<ValueT>("value", valueMappingFunction)
					.Do(context => dictionaryProperty.GetValue(context.GetVariable<ParentT>("parent")).Add(
							context.GetVariable<KeyT>("key"),
							context.GetVariable<ValueT>("value")
						)
					)
				)
			);

			return mapping;
		}

		/// <summary>
		/// Shortcut for mapping a list from a subquery.
		/// </summary>
		public static Mapping<ParentT> MapListFromSubquery<ParentT, ItemT>(
			this Mapping<ParentT> mapping,
			EntityProperty<ParentT, List<ItemT>> listProperty,
			string subqueryName,
			Action<Mapping<ParentT>> parentMappingFunction,
			Action<Mapping<ItemT>> itemMappingFunction
		)
		{

			mapping.Map<List<ItemT>>(listProperty, list => list
				.Subquery<ItemT>(subqueryName, subquery => subquery
					.Map<ParentT>("parent", parentMappingFunction)
					.Map<ItemT>("item", itemMappingFunction)
					.Do(context =>
					{
						var parent = context.GetVariable<ParentT>("parent");
						var item = context.GetVariable<ItemT>("item");
						var l = listProperty.GetValue(parent);
						if (l == null)
						{
							l = new List<ItemT>();
							listProperty.SetValue(parent, l);
						}

						l.Add(item);

						// This has no real value but helps makes sense of this cruel world
						context.Target = item;
					})
				)
			);

			return mapping;
		}
	}

	[Serializable]
	public class EdgeTemplateException : Exception
	{
		public EdgeTemplateException() { }
		public EdgeTemplateException(string message) : base(message) { }
		public EdgeTemplateException(string message, Exception inner) : base(message, inner) { }
		protected EdgeTemplateException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
