﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using Eggplant.Entities.Queries;

namespace Edge.Data.Objects
{
	public static class EdgeObjectsUtility
	{
		public static EntitySpace EntitySpace { get; private set; }

		static EdgeObjectsUtility()
		{
			EdgeObjectsUtility.EntitySpace = new EntitySpace();
		}

		public static string GetEdgeTemplate(string fileName, string templateName)
		{
			const string tplSeparatorPattern = @"^--\s*#\s*TEMPLATE\s+(.*)$";
			Regex tplSeparatorRegex = new Regex(tplSeparatorPattern, RegexOptions.Singleline);

			var templateString = new StringBuilder();
			Assembly asm = Assembly.GetExecutingAssembly();
			using (StreamReader reader = new StreamReader(asm.GetManifestResourceStream(@"Edge.Data.Objects.Queries." + fileName)))
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

			return templateString.ToString();
		}

		#region ParseEdgeTemplate
		// ===========================
		
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

		// ===========================
		#endregion
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
