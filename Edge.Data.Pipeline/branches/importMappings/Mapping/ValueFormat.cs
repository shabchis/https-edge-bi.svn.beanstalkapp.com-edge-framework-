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
	/// Contains instructions on how to output to string various components including read commands and C# expressions.
	/// </summary>
	public class ValueFormat
	{
		/// <summary>
		/// The parent map command.
		/// </summary>
		public MapCommand Parent { get; private set; }
		
		// Matches {stuff} but not \{stuff}
		static Regex _componentParser = new Regex(@"(?<!\\)\{([^\}]*)\}");

		/// <summary>
		/// 
		/// </summary>
		public List<object> Components { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="expression"></param>
		internal ValueFormat(MapCommand parent, string expression)
		{
			this.Parent = parent;

			int indexLast = 0;
			Match comp = _componentParser.Match(expression);
			while (comp != null)
			{
				if (!comp.Groups[0].Success)
					throw new MappingConfigurationException(String.Format("'{0}' is not a valid component of a value expression.", comp.Value));

				// Get the string component between this match and the previos
				if (indexLast < comp.Index)
				{
					string str = expression.Substring(indexLast, comp.Index - indexLast);
					this.Components.Add(new StringComponent(this.Parent, str));
				}
				
				indexLast = comp.Index + comp.Length + 1;

				// Construct the eval component
				string eval = comp.Groups[0].Value.Trim();
				this.Components.Add(new EvalComponent(this.Parent, eval));

				// Move to the next
				comp = comp.NextMatch();
			}

			// Pick up the remainer of the string
			if (indexLast < expression.Length-1)
				this.Components.Add(new StringComponent(this.Parent, expression.Substring(indexLast)));

		}

		public string Output(MappingContext context)
		{
			var output = new StringBuilder();
			
			foreach (ValueComponent component in this.Components)
				output.Append(component.Ouput(context));

			string value = output.ToString();
			return value;
		}
	}

	/// <summary>
	/// Base class for components used to insert values into map command "To" or "Value" attributes.
	/// </summary>
	public abstract class ValueComponent
	{
		public MapCommand Parent
		{
			get;
			private set;
		}

		public ValueComponent(MapCommand parent)
		{
			this.Parent = parent;
		}

		public abstract string Ouput(MappingContext context);
	}

	/// <summary>
	/// An format component that uses a C# expression. Can reference read commands and read command fragments as normal variables.
	/// </summary>
	public class EvalComponent : ValueComponent
	{
		private string _evalID;

		internal EvaluatorExpression Expression { get; private set; }
		public string ExpressionString { get; private set; }

		internal EvalComponent(MapCommand parent, string expression):base(parent)
		{
			this.ExpressionString = expression;
			_evalID = String.Format("Expression_{0}", parent.Root.NextEvalID++);

			// Add a dynamic var with the name of the command, sort by name for later
			var evalvars = new List<EvaluatorVariable>();
			foreach (ReadCommand command in this.Parent.Parent.InheritedReads.Values.OrderBy(cmd => cmd.Name))
				evalvars.Add(new EvaluatorVariable(command.Name));

			// Create the expression, will be compiled later
			this.Expression = new EvaluatorExpression(
				name:			_evalID,
				expression: expression,
				returnType:		typeof(Object),
				variables:		evalvars.ToArray()
			);
		}

		public override string Ouput(MappingContext context)
		{
			throw new NotImplementedException();
		}

		public object Eval(MappingContext context)
		{
			return this.Parent.Root.Eval.Evaluate<object>(_evalID);//, context.EvalVariables);
		}

	}

	/// <summary>
	/// An format component that outputs a static string.
	/// </summary>
	public class StringComponent : ValueComponent
	{
		public string Value { get; private set; }

		internal StringComponent(MapCommand parent, string value): base(parent)
		{
			this.Value = value;
		}

		public override string Ouput(MappingContext context)
		{
			throw new NotImplementedException();
		}
	}

}
