using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using Edge.Core.Data;
using Edge.Core.Data.Proxy;
using Edge.Core.Services;

namespace Edge.Core.Data.Proxy
{
	[ServiceContract]
	public interface IProxyServer
	{
		[OperationContract]
		[NetDataContract]
		ProxyResult ProcessRequest(ProxyRequest request);
	}

	/// <summary>
	/// WCF service that process proxy requests sent by ProxyClient.
	/// </summary>
	[Obsolete("Proxy behavior should not be used before redesign using Persistence model.")]
	public class ProxyServer: IProxyServer
	{
		#region Static

		private static Dictionary<OperationContext, ProxyServer> _instances =
			new Dictionary<OperationContext, ProxyServer>();

		/// <summary>
		/// Gets the result object for the current operation.
		/// </summary>
		public static ProxyServer Current
		{
			get
			{
				if (OperationContext.Current == null)
					return null;
				else
					return _instances[OperationContext.Current];
			}
		}

		public static bool InProgress
		{
			get { return Current != null; }
		}

		#endregion

		#region Instance

		ProxyResult _result = null;
		internal ProxyRequestAction CurrentAction = null;
		
		public ProxyResult Result
		{
			get { return _result; }
		}

		public ProxyResult ProcessRequest(ProxyRequest request)
		{
			_instances.Add(OperationContext.Current, this);

			// Proxy result used to store the results of the actions
			ProxyResult result = new ProxyResult(request);
			_result = result;

			// Action loop
			for (int currentIndex = 0; currentIndex < request.Actions.Count; currentIndex++)
			{
				ProxyRequestAction action = request.Actions[currentIndex];
				CurrentAction = action;

				try
				{
					// Replace proxy object parameters with actual values (from previous actions)
					for (int p = 0; p < action.Parameters.Length; p++)
					{
						if (action.Parameters[p] is IDataBoundObject)
						{
							IDataBoundObject o = (IDataBoundObject) action.Parameters[p];
							if (o.ProxyActionIndex < currentIndex)
							{
								// Parameter is a proxy object referencing a previous step, so replace with that step's result
								action.Parameters[p] = result[o.ProxyActionIndex].ReturnValue;
							}
							else
							{
								throw new InvalidOperationException(String.Format("Cannot execute action #{0} because it requires the result of action #{1}", currentIndex, o.ProxyActionIndex));
							}
						}
					}

					// Get the method to invoke
					Type targetType = action.Caller != null ? action.Caller.GetType() : Type.GetType(action.MethodType);
					MethodInfo method = targetType.GetMethod(action.MethodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

					// Perform the action and store the result
					object val = method.Invoke(action.Caller, action.Parameters);
					result[currentIndex].ReturnValue = val;
				}
				catch (Exception ex)
				{
					result[currentIndex].ReturnValue =  ex is TargetInvocationException ? 
							((TargetInvocationException) ex).InnerException : 
							ex;

					// If request is specified as a transaction, break execution
					if (request.StopOnError)
						break;
				}
			}

			// Done, remove from static results and return
			_result = null;
			this.CurrentAction = null;
			_instances.Remove(OperationContext.Current);

			return result;
		}

		#endregion
	}
}
