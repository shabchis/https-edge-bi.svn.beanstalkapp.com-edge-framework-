using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Edge.Core.Utilities;
using System.Xml;

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
		static Regex _componentParser = new Regex(@"(?<!\\)\{(?<eval>[^\}]*)\}", RegexOptions.ExplicitCapture);

		/// <summary>
		/// 
		/// </summary>
		public List<ValueComponent> Components { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="expression"></param>
		internal ValueFormat(MapCommand parent, string expression, XmlReader xml)
		{
			this.Parent = parent;
			this.Components = new List<ValueComponent>();

			int indexLast = 0;
			Match comp = _componentParser.Match(expression);
			while (comp != null && comp.Success)
			{
				if (!comp.Groups["eval"].Success)
					throw new MappingConfigurationException(String.Format("'{0}' is not a valid component of a value expression.", comp.Value));

				// Get the string component between this match and the previos
				if (indexLast < comp.Index)
				{
					string str = expression.Substring(indexLast, comp.Index - indexLast);
					this.Components.Add(new StringComponent(this.Parent, str));
				}

				indexLast = comp.Index + comp.Length;

				// Construct the eval component
				string eval = comp.Groups["eval"].Value.Trim();
				this.Components.Add(new EvalComponent(this.Parent, eval, xml));

				// Move to the next
				comp = comp.NextMatch();
			}

			// Pick up the remainer of the string
			if (indexLast <= expression.Length-1)
				this.Components.Add(new StringComponent(this.Parent, expression.Substring(indexLast)));

		}

		public object GetOutput(MappingContext context)
		{
			object output;
			if (this.Components.Count > 1)
			{
				var asString = new StringBuilder();

				foreach (ValueComponent component in this.Components)
					asString.Append(component.GetOuput(context));

				output = asString.ToString();
			}
			else if (this.Components.Count == 1)
			{
				object temp = this.Components[0].GetOuput(context);
				if (temp is ReadResult && ((ReadResult)temp).Values == null)
					output = ((ReadResult)temp).FieldValue;
				else
					output = temp;
			}
			else
			{
				output = null;
			}

			return output;
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

		public abstract object GetOuput(MappingContext context);
	}

	/// <summary>
	/// An format component that uses a C# expression. Can reference read commands and read command fragments as normal variables.
	/// </summary>
	public class EvalComponent : ValueComponent
	{
		private string _evalID;

		internal EvaluatorExpression Expression { get; private set; }
		public string ExpressionString { get; private set; }

		internal EvalComponent(MapCommand parent, string expression, XmlReader xml, bool inheritedReadOnly = false)
			: base(parent)
		{
			this.ExpressionString = expression;
			_evalID = String.Format("Expression_{0}", parent.Root.NextEvalID++);

			// Add a dynamic var with the name of the command, sort by name for later
			var evalvars = new List<EvaluatorVariable>();
			foreach (ReadCommand command in this.Parent.InheritedReads.Values.OrderBy(cmd => cmd.VarName))
			{
				if (inheritedReadOnly && this.Parent.ReadCommands.Contains(command))
					continue;

				evalvars.Add(new EvaluatorVariable(command.VarName));
			}

			// Create the expression, will be compiled later
			this.Expression = new EvaluatorExpression(
				name: _evalID,
				expression: expression,
				returnType: typeof(Object),
				variables: evalvars.ToArray()
			)
			{
				LineFile = parent.Root.SourcePath,
				LineNumber = xml is XmlTextReader ? ((XmlTextReader)xml).LineNumber : 0
			};

			this.Parent.Root.Eval.Expressions.Add(this.Expression);
		}

		public override object GetOuput(MappingContext context)
		{
			return this.GetOuput(context, false);
		}

		public object GetOuput(MappingContext context, bool inheritedOnly)
		{
			// Build a list of vars, including null if nothing found
			var evalVars = new List<object>();
			foreach (ReadCommand read in this.Parent.InheritedReads.Values.OrderBy(cmd => cmd.VarName))
			{
				if (inheritedOnly && this.Parent.ReadCommands.Contains(read))
					continue;

				ReadResult result;
				if (!context.ReadResults.TryGetValue(read, out result))
					result = null;
				evalVars.Add(result);
			}

			// TODO: if result not found and is required - throw exception - otherwise don't evaluate
			return this.Parent.Root.Eval.Evaluate<object>(_evalID, evalVars.ToArray());
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

		public override object GetOuput(MappingContext context)
		{
			return this.Value;
		}
	}

}
