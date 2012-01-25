using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Edge.Core.Utilities;

namespace Edge.Data.Pipeline.Mapping
{
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

			// Table of recognized variables
			var reads = new List<string>();
			MappingContainer container = parent;
			while (container != null)
			{
				//foreach (ReadCommand read in container.ReadCommands)
					//varList.Add(read
			}

			MatchCollection components = _componentParser.Matches(expression);
			foreach (Match comp in components)
			{
				if (!comp.Groups[0].Success)
					continue;

				ValueExpressionComponent component;
				string compStr = comp.Groups[0].Value.Trim();
				if (compStr.StartsWith("="))
					component = new EvalComponent(this, String.Format("Eval{0}", parent.Root.NextEvalID++), compStr.Substring(1), reads);
				else if (compStr.Contains(':'))
					component = new ValueLookupComponent(this) { Lookup = new ValueLookup(compStr) };
				else
					component = new ReadComponent(this) { ReadCommand = reads[compStr] };

				this.Components.Add(component);
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

	public class ValueLookupComponent : ValueExpressionComponent
	{
		public ValueLookup Lookup;
	}

	public class EvalComponent : ValueExpressionComponent
	{
		private string _evalID { get; private set; }
		public EvaluatorExpression Expression;
		static Regex _escaper = new Regex("[^a-z0-9_]", RegexOptions.IgnoreCase);

		internal EvalComponent(ValueExpression parent, string expression, string evalID, List<string> variables):base(parent)
		{
			this._evalID = evalID;

			// All vars are strings
			var evalvars = new EvaluatorVariable[variables.Count];
			for (int i = 0; i < variables.Count; i++)
				evalvars[i] = new EvaluatorVariable(_escaper.Replace(variables[i], "_"), typeof(string));

			// Create the expression, will be compiled later
			this.Expression = new EvaluatorExpression(evalID, expression, typeof(object), evalvars);
		}

		public override string Ouput()
		{
			//return _root.Eval.Evaluate<object>(_evalID, 
		}

	}

	public class ReadComponent : ValueExpressionComponent
	{
		public ReadCommand ReadCommand;
	}

	public class StringComponent : ValueExpressionComponent
	{
		public string Value;
	}

}
