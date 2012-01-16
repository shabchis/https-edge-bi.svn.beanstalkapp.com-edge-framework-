using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace Edge.Data.Pipeline.Mapping
{
	public class ValueExpression
	{
		public List<ValueExpressionComponent> Components;

		public T Ouput<T>()
		{
			return (T)this.Output(typeof(T));
		}

		public object Output(Type ouputType)
		{
			bool isstring = ouputType == typeof(string);
			TypeConverter converter = null;

			if (!isstring)
			{
				converter = TypeDescriptor.GetConverter(ouputType);
				if (converter == null)
					throw new MappingException(String.Format("Cannot convert string to {0}.", ouputType.FullName));
			}

			var output = new StringBuilder();

			foreach (ValueExpressionComponent component in this.Components)
			{
				output.Append(component.Ouput());
			}

			string value = output.ToString();
			object returnValue;

			if (isstring)
			{
				// avoid compiler errors
				object o = output.ToString();
				returnValue = o;
			}
			else
			{
				if (!converter.IsValid(value))
					throw new MappingException(String.Format("'{0}' is not a valid value for {1}", value, ouputType.FullName));
				else
					returnValue = converter.ConvertFrom(value);
			}

			return returnValue;
		}
	}

	public abstract class ValueExpressionComponent
	{
		public abstract string Ouput();
	}

	public class FunctionInvokeComponent : ValueExpressionComponent
	{
		public string FunctionName;
		public List<ValueExpression> Parameters;

		public override string Ouput()
		{
			throw new NotImplementedException();
		}
	}

	public class EvalComponent
	{
		public Evaluator Eval;
		public List<ValueExpression> Variables;
	}

	public class ReadSourceOuputComponent
	{
		public ReadCommand ReadSource;
	}

	public class StringComponent
	{
		public string String;
	}

}
