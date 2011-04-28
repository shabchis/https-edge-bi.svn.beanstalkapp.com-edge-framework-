using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Text;
using System.Reflection;

namespace Edge.Core.Utilities
{
	/// <summary>
	/// Evaluates strings as C# expressions.
	/// </summary>
	public class Evaluator
	{
		#region Fields
		/*=========================*/

		const string DefaultMethodName = "Expression";
		//Type _compiledType = null;
		object _compiled = null;

		/*=========================*/
		#endregion

		#region Constructors
		/*=========================*/

		public Evaluator(EvaluatorExpression[] expressions): this(expressions, null)
		{
		}

		public Evaluator(EvaluatorExpression[] expressions, EvaluatorVariable[] globalVariables)
		{
			ConstructEvaluator(expressions, globalVariables);
		}

		public Evaluator(string expression, Type returnType, EvaluatorVariable[] globalVariables)
		{
			EvaluatorExpression[] items = { new EvaluatorExpression(DefaultMethodName, expression, returnType) };
			ConstructEvaluator(items, globalVariables);
		}

		public Evaluator(EvaluatorExpression expression)
		{
			EvaluatorExpression[] expressions = { expression };
			ConstructEvaluator(expressions, null);
		}

		/*=========================*/
		#endregion

		#region Internal
		/*=========================*/

		private void ConstructEvaluator(EvaluatorExpression[] expressions, EvaluatorVariable[] globalVariables)
		{
			CSharpCodeProvider comp = new CSharpCodeProvider();
			CompilerParameters cp = new CompilerParameters();
			cp.ReferencedAssemblies.Add("system.dll");
			cp.GenerateExecutable = false;
			cp.GenerateInMemory = true;

			StringBuilder code = new StringBuilder();
			code.Append(@"
			using System;

			namespace Easynet.Edge.Runtime
			{
				public class Eval
				{
			");

			if (globalVariables != null)
			{
				foreach (EvaluatorVariable variable in globalVariables)
				{
					code.Append(variable);
				}
			}
	
			foreach (EvaluatorExpression expression in expressions)
			{
				code.Append(expression);
			}

			code.Append(@"
				}
			}"
			);

			CompilerResults cr = comp.CompileAssemblyFromSource(cp, code.ToString());
			if (cr.Errors.HasErrors)
			{
				StringBuilder error = new StringBuilder();
				foreach (CompilerError err in cr.Errors)
				{
					error.AppendFormat("{0}\n", err.ErrorText);
				}
				throw new Exception("Error compiling expression:\n\n" + error.ToString());
			}
			
			Assembly a = cr.CompiledAssembly;
			_compiled = a.CreateInstance("Easynet.Edge.Runtime.Eval");
		}
		
		/*=========================*/
		#endregion

		#region Instance methods
		/*=========================*/

		public int EvaluateInt(string name)
		{
			return (int) Evaluate(name);
		}

		public int EvaluateInt()
		{
			return EvaluateInt(DefaultMethodName);
		}

		public string EvaluateString(string name)
		{
			return (string) Evaluate(name);
		}

		public string EvaluateString()
		{
			return EvaluateString(DefaultMethodName);
		}

		public bool EvaluateBool(string name)
		{
			return (bool) Evaluate(name);
		}

		public bool EvaluateBool()
		{
			return EvaluateBool(DefaultMethodName);
		}

		public object Evaluate(string name)
		{
			MethodInfo mi = _compiled.GetType().GetMethod(name);
			return mi.Invoke(_compiled, null);
		}

		public object Evaluate()
		{
			return Evaluate(DefaultMethodName);
		}

		/*=========================*/
		#endregion

		#region Static methods
		/*=========================*/

		static public TReturnType Eval<TReturnType>(string expression, EvaluatorVariable[] variables)
		{
			Evaluator eval = new Evaluator(expression, typeof(TReturnType), variables);
			return (TReturnType) eval.Evaluate(DefaultMethodName);
		}

		/*=========================*/
		#endregion
	}

	/// <summary>
	/// 
	/// </summary>
	public class EvaluatorExpression
	{
		public readonly string Name;
		public readonly string Expression;
		public readonly Type ReturnType;
		public readonly EvaluatorVariable[] Variables;

		public EvaluatorExpression(string name, string expression, Type returnType): this(name, expression, returnType, null)
		{
		}

		public EvaluatorExpression(string name, string expression, Type returnType, EvaluatorVariable[] variables)
		{
			ReturnType = returnType;
			Expression = expression;
			Name = name;
			Variables = variables;
		}

		public override string ToString()
		{
			string variablesOutput = string.Empty;
			if (Variables != null)
			{
				foreach (EvaluatorVariable variable in Variables)
				{
					variablesOutput += variable.ToString();
				}
			}

			return string.Format(@"
				public {0} {1}()
				{{
					{3}

					return {2};
				}}
				",
				ReturnType.FullName,
				Name,
				Expression,
				variablesOutput);
		}

	}

	/// <summary>
	/// 
	/// </summary>
	public class EvaluatorVariable
	{
		public readonly string Name;
		public readonly string Value;
		public readonly Type VariableType;

		public EvaluatorVariable(string name, string value, Type variableType)
		{
			Name = name;
			Value = value;
			VariableType = variableType;
		}

		public override string ToString()
		{
			return string.Format(@"
				{0} {1} = {2};
				",
				VariableType.FullName,
				Name,
				Value);
		}
	}
}
