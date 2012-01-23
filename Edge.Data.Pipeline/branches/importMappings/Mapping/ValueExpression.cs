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
		//static Regex

		/// <summary>
		/// 
		/// </summary>
		public List<ValueExpressionComponent> Components { get; private set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="expression"></param>
		public ValueExpression(string expression)
		{
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
		public abstract string Ouput();
	}

	public class ValueLookupComponent : ValueExpressionComponent
	{
		public ValueLookup Lookup;
	}

	public class EvalComponent
	{
		public EvaluatorExpression Expression;
	}

	public class ReadComponent
	{
		public ReadCommand ReadCommand;
	}

	public class StringComponent
	{
		public string Value;
	}

}
