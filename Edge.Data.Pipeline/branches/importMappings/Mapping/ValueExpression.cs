using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline.Mapping
{
	/// <summary>
	/// Contains instructions on how to output various components including read commands and C# expressions.
	/// </summary>
	public class ValueExpression
	{
		/// <summary>
		/// The parent map command.
		/// </summary>
		public MapCommand Parent { get; private set; }

		static Regex _componentParser = new Regex(@"\{(?[^\}]*)\}");

		/// <summary>
		/// 
		/// </summary>
		public List<ValueExpressionComponent> Components { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="expression"></param>
		internal ValueExpression(MapCommand parent, string expression)
		{
			this.Parent = parent;

			int indexLast = 0;
			Match comp = _componentParser.Match(expression);
			while (comp != null)
			{
				if (!comp.Groups[0].Success)
					throw new MappingConfigurationException(String.Format("'{0}' is not a valid component of a value expression.", comp.Value));

				// Get the string component between this match and the previos
				string filler = expression.Substring(indexLast, comp.Index - indexLast);
				
				if (filler.Length > 0)
					this.Components.Add(new StringComponent(this, filler));
				
				indexLast = comp.Index + 1;

				// Construct the special component
				ValueExpressionComponent component;
				string compStr = comp.Groups[0].Value.Trim();
				
				if (compStr.StartsWith("="))
					component = new EvalComponent(this, String.Format("eval_{0}", parent.Root.NextEvalID++), compStr.Substring(1));
				else if (compStr.Contains(':'))
					component = new ValueLookupComponent(this, compStr);
				else
					component = new ReadComponent(this, compStr);

				this.Components.Add(component);

				// Move to the next
				comp = comp.NextMatch();
			}

		}

		public string Output()
		{
			var output = new StringBuilder();
			
			foreach (ValueExpressionComponent component in this.Components)
				output.Append(component.Ouput());

			string value = output.ToString();
			return value;
		}
	}

	/// <summary>
	/// Base class for expression components.
	/// </summary>
	public abstract class ValueExpressionComponent
	{
		public ValueExpression ParentExpression
		{
			get;
			private set;
		}

		public ValueExpressionComponent(ValueExpression parent)
		{
			this.ParentExpression = parent;
		}

		public abstract string Ouput();
	}

	/// <summary>
	/// An expression component that triggers a value lookup callback so the caller can supply their own value using the supplied parameters.
	/// </summary>
	public class ValueLookupComponent : ValueExpressionComponent
	{
		public ValueLookup Lookup { get; private set; }

		internal ValueLookupComponent(ValueExpression parent, string lookupExpression): base(parent)
		{
			this.Lookup = new ValueLookup(lookupExpression);
		}
	}

	/// <summary>
	/// An expression that evaluates a C# expression. Can reference read commands and read command fragments as normal variables.
	/// </summary>
	public class EvalComponent : ValueExpressionComponent
	{
		private string _evalID;
		const string _fragmentFormat = "_FRAG_";

		internal EvaluatorExpression Expression { get; private set; }
		public string ExpressionString { get; private set; }

		internal EvalComponent(ValueExpression parent, string expression, string evalID):base(parent)
		{
			this._evalID = evalID;
			this.ExpressionString = expression;

			// This will replace dots with _fragmentFormat
			// Example: ((?<read>bobby)(\.(?<frag>mcgee|dylan|marley|vinton))*)|((?<read>charlie)(\.(?<frag>parker|chaplin|mcguerra))*)
			var varRegex = new StringBuilder();

			// Create vars list
			var evalvars = new List<EvaluatorVariable>();

			// Add variables by name so as to maintain the same order
			foreach (ReadCommand command in this.ParentExpression.Parent.InheritedReads.Values.OrderBy(cmd => cmd.Name))
			{
				// build a regex that will fix the var names
				bool regexReplace = command.RegexFragments != null && command.RegexFragments.Length > 0;
				if (regexReplace)
				{
					if (varRegex.Length > 0)
						varRegex.Append("|");

					varRegex
						.Append(@"(")
						.Append(command.Name)
						.Append(@"(?<dot>\.)(");
				}
				
				// Add a var with the name of the command
				evalvars.Add(new EvaluatorVariable(command.Name, typeof(String)));

				for (int i = 0; i < command.RegexFragments.Length; i++)
				{
					string fragment = command.RegexFragments[i];

					// Add vars for each
					evalvars.Add(new EvaluatorVariable(String.Format("{0}" + _fragmentFormat + "{1}", command.Name, fragment), typeof(String)));

					if (regexReplace)
					{
						if (i > 0)
							varRegex.Append("|");
						varRegex.Append(fragment);
					}
				}

				if (regexReplace)
				{
					varRegex.Append("))");
				}
			}

			// Escape the expression if necessary
			string escapedExpression = varRegex.Length == 0 ?
				expression :
				new Regex(varRegex.ToString(), RegexOptions.ExplicitCapture).Replace(expression, _fragmentFormat);

			// Create the expression, will be compiled later
			this.Expression = new EvaluatorExpression(
				name:			evalID,
				expression:		escapedExpression,
				returnType:		typeof(Object),
				variables:		evalvars.ToArray()
			);
		}

		public override string Ouput()
		{
			//return _root.Eval.Evaluate<object>(_evalID, 
		}

	}

	/// <summary>
	/// An expression component that outputs the results of a read command.
	/// </summary>
	public class ReadComponent : ValueExpressionComponent
	{
		public ReadCommand ReadCommand { get; private set; }
		public string Fragment { get; private set; }

		internal ReadComponent(ValueExpression parent, string readExpression):base(parent)
		{
			string[] parsed = readExpression.Split(new char[]{'.'}, 2);
			if (parsed.Length < 1)
				throw new MappingConfigurationException(String.Format("'{0}' is not a valid read command reference.", readExpression));

			string cmdName = parsed[0];
			string fragment = parsed.Length > 1 ? parsed[1] : null;

			if (!ReadCommand.IsValidVarName(cmdName) || (fragment != null && !ReadCommand.IsValidVarName(fragment)))
				throw new MappingConfigurationException(String.Format("'{0}' is not a valid read command reference.", readExpression));
	
			// Find the matching command
			ReadCommand cmd;
			if (!this.ParentExpression.Parent.InheritedReads.TryGetValue(cmdName, out cmd))
				throw new MappingConfigurationException(String.Format("The read command '{0}' does not exist.", cmdName));
			this.ReadCommand = cmd;
			
			// Find the matching fragment
			if (fragment != null && cmd.RegexFragments.Count(f => f == fragment) < 1)
				throw new MappingConfigurationException(String.Format("The fragment '{0}' does not exist in the read command '{1}'.", cmdName, fragment));
			this.Fragment = fragment;
		}
	}

	/// <summary>
	/// An expression component that outputs a static string.
	/// </summary>
	public class StringComponent : ValueExpressionComponent
	{
		public string Value { get; private set; }

		internal StringComponent(ValueExpression parent, string value):base(parent)
		{
			this.Value = value;
		}

	}

}
