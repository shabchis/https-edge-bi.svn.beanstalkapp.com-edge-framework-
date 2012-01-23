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

			namespace Edge.Runtime
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
			_compiled = a.CreateInstance("Edge.Runtime.Eval");
		}
		
		/*=========================*/
		#endregion

		#region Instance methods
		/*=========================*/

		public T Evaluate<T>(string name, object[] @params = null)
		{
			MethodInfo mi = _compiled.GetType().GetMethod(name);
			return (T) mi.Invoke(_compiled, @params);
		}

		public T Evaluate<T>()
		{
			return Evaluate<T>(DefaultMethodName);
		}

		public T Evaluate<T>(params object[] @params)
		{
			return Evaluate<T>(DefaultMethodName, @params);
		}

		/*=========================*/
		#endregion

		#region Static methods
		/*=========================*/

		static public T Eval<T>(string expression, EvaluatorVariable[] variables = null, object[] variableValues = null)
		{
			Evaluator eval = new Evaluator(expression, typeof(T), variables);
			return eval.Evaluate<T>(DefaultMethodName, variableValues);
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
			var variablesOutput = new StringBuilder();
			if (Variables != null)
			{
				for (int i = 0; i < Variables.Length; i++ )
				{
					EvaluatorVariable variable = Variables[i];
					variablesOutput.Append(variable.ToString());
					if (i < Variables.Length - 1)
						variablesOutput.Append(", ");

				}
			}

			return string.Format(@"
				public {0} {1}({2})
				{{
					return {3};
				}}
				",
				ReturnType.FullName,
				Name,
				variablesOutput,
				Expression);
		}

	}

	/// <summary>
	/// 
	/// </summary>
	public class EvaluatorVariable
	{
		public readonly string Name;
		public readonly Type VariableType;

		public EvaluatorVariable(string name, Type variableType)
		{
			Name = name;
			VariableType = variableType;
		}

		public override string ToString()
		{
			return string.Format(@"{0} {1}", VariableType.FullName, Name);
		}
	}
}
