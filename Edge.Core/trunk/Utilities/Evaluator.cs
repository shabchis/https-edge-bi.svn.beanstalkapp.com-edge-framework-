using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Text;
using System.Reflection;
using System.Collections.Generic;

namespace Edge.Core.Utilities
{
	/// <summary>
	/// Evaluates strings as C# expressions.
	/// </summary>
	public class Evaluator
	{

		const string DefaultMethodName = "Expression";
		object _compiled = null;

		public readonly List<EvaluatorExpression> Expressions = new List<EvaluatorExpression>();
		public readonly Dictionary<string, Delegate> Externals = new Dictionary<string, Delegate>();
		public readonly List<string> ReferencedAssemblies = new List<string>();
		public readonly List<string> UsingNamespaces = new List<string>();

		public void Compile()
		{
			// TODO: code access security, limit access to IO/network/etc.
			// http://stackoverflow.com/questions/5997995/in-net-4-0-how-do-i-sandbox-an-in-memory-assembly-and-execute-a-method

			StringBuilder code = new StringBuilder();
			code.Append(@"
			using System;
			");
			
			foreach (string ns in this.UsingNamespaces)
			{
				code.AppendFormat("using {0};\n", ns);
			}

			code.Append(@"
			namespace Edge.Runtime
			{
				public class Eval: MarshalByRefObject
				{			
					public event Func<string, object[], object> ExternalExecute;

					");
					
					foreach(KeyValuePair<string, Delegate> function in this.Externals)
					{
						code.AppendFormat(@"
					{0} {1}(params object[] args)
					{{
						return ({0}) ExternalExecute(""{1}"", args);
					}}
						", function.Value.Method.ReturnType.FullName, function.Key);
					}


					foreach (EvaluatorExpression expression in this.Expressions)
					{
						code.Append(expression);
					}


					code.Append(@"
				}
			}"
			);

			CSharpCodeProvider comp = new CSharpCodeProvider();
			CompilerParameters cp = new CompilerParameters();
			cp.ReferencedAssemblies.Add("System.dll");
			cp.ReferencedAssemblies.Add("System.Core.dll");
			cp.ReferencedAssemblies.Add("Microsoft.CSharp.dll");
			cp.GenerateExecutable = false;
			cp.GenerateInMemory = true; 
			CompilerResults cr = comp.CompileAssemblyFromSource(cp, code.ToString());
			if (cr.Errors.HasErrors)
			{
				StringBuilder error = new StringBuilder();
				foreach (CompilerError err in cr.Errors)
				{
					error.AppendFormat("{0} (line {1})\n", err.ErrorText, err.Line);
				}
				throw new Exception("Error compiling expression:\n\n" + error.ToString());
			}
			
			Assembly a = cr.CompiledAssembly;
			_compiled = a.CreateInstance("Edge.Runtime.Eval");
		}
		

		public T Evaluate<T>(string expressionName, object[] @params = null)
		{
			if (_compiled == null)
				throw new EvaluatorException("Compile() must be called on the evaluator before it can be used.");

			MethodInfo mi = _compiled.GetType().GetMethod(expressionName);
			if (mi == null)
				throw new EvaluatorException(String.Format("There is no expression with the name '{0}' in the evaluator.", expressionName));

			try { return (T)mi.Invoke(_compiled, @params); }
			catch (Exception ex)
			{
				throw new EvaluatorException(String.Format("Expression evaluation failed: {0}.", ex is TargetInvocationException ? ex.InnerException.Message : ex.Message), ex);
			}
		}

		public T Evaluate<T>()
		{
			return Evaluate<T>(DefaultMethodName);
		}

		public T Evaluate<T>(params object[] @params)
		{
			return Evaluate<T>(DefaultMethodName, @params);
		}


		/// <summary>
		/// 
		/// </summary>
		static public T Eval<T>(string expression, EvaluatorVariable[] variables = null, object[] variableValues = null)
		{
			var eval = new Evaluator();
			eval.Expressions.Add(new EvaluatorExpression(DefaultMethodName, expression, typeof(T), variables));
			eval.Compile();
			return eval.Evaluate<T>(DefaultMethodName, variableValues);
		}

	}

	/// <summary>
	/// 
	/// </summary>
	public class EvaluatorExpression
	{
		public readonly string Name;
		public readonly string Expression;
		public readonly Type ReturnType;
		public string LineFile;
		public int LineNumber;
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
					{4}
					public {0} {1}({2})
					{{
						return {3};
					}}
				",
				ReturnType.FullName,
				Name,
				variablesOutput,
				Expression,
				LineFile != null & LineNumber > 0 ? String.Format("#line {0} \"{1}\"", LineNumber, LineFile) : null
				);
		}

	}

	/// <summary>
	/// 
	/// </summary>
	public class EvaluatorVariable
	{
		public readonly string Name;
		public readonly Type VariableType = null;
		public readonly bool IsDynamic = false;

		public EvaluatorVariable(string name, Type variableType)
		{
			Name = name;
			VariableType = variableType;
		}

		public EvaluatorVariable(string name)
		{
			Name = name;
			IsDynamic = true;
		}

		public override string ToString()
		{
			return string.Format(@"{0} {1}", IsDynamic ? "dynamic" : VariableType.FullName, Name);
		}
	}

	[Serializable]
	public class EvaluatorException : Exception
	{
		public EvaluatorException() { }
		public EvaluatorException(string message) : base(message) { }
		public EvaluatorException(string message, Exception inner) : base(message, inner) { }
		protected EvaluatorException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
