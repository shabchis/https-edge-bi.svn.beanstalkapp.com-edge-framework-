using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Reflection;
using System.Data;

namespace Edge.Core.Data.Proxy
{
	[Serializable]
	public class ProxyRequest: IDisposable
	{
		[NonSerialized]
		public Action Send;
		
		[NonSerialized]
		public bool Canceled = false;

		private List<ProxyRequestAction> _actions = new List<ProxyRequestAction>();
		public readonly bool IsAsync;
		public readonly bool StopOnError;
	
		public ProxyRequest(bool async)
		{
			IsAsync = async;
		}

		void IDisposable.Dispose()
		{
			if (!Canceled && Send != null)
				Send();
		}

		public List<ProxyRequestAction> Actions
		{
			get { return _actions; }
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="caller">The object on which to perform the method. Null if the method is static.</param>
		/// <param name="method">The method to perform on the caller, or statically.</param>
		/// <param name="target">The target DataItem that will contain the result. It is populated after</param>
		/// <param name="parameters"></param>
		/// <returns></returns>
		public ProxyRequestAction AddAction(object caller, MethodBase method, IDataBoundObject target, object[] parameters)
		{
			ProxyRequestAction action = new ProxyRequestAction(caller, target, method.DeclaringType.AssemblyQualifiedName, method.Name, parameters);
			_actions.Add(action);

			//if (target != null)
			//	target.ActionIndex = _actions.IndexOf(action);

			action.ActionIndex = _actions.IndexOf(action);
			return action;
		}

		public ProxyRequestAction AddAction(object caller, MethodBase method)
		{
			return AddAction(caller, method, null, new object[]{});
		}
	}

	[Serializable]
	public class ProxyRequestAction
	{
		public readonly string MethodType;
		public readonly string MethodName;
		public readonly object Caller;
		public readonly object[] Parameters;
		public int ActionIndex = -1;

		[NonSerialized]
		public readonly IDataBoundObject Target;

		[NonSerialized]
		public Action OnComplete;

		internal ProxyRequestAction(object caller, IDataBoundObject target, string methodType, string methodName, object[] parameters)
		{
			Caller = caller;
			Target = target;
			MethodType = methodType;
			MethodName = methodName;
			Parameters = parameters;
		}
	}

	[Serializable]
	public class ProxyResult
	{
		ProxyResultActionData[] _data;

		public ProxyResult(ProxyRequest request)
		{
			_data = new ProxyResultActionData[request.Actions.Count];
			for(int i = 0; i < _data.Length; i++)
				_data[i] = new ProxyResultActionData(i);
		}

		public ProxyResultActionData this[int actionIndex]
		{
			get { return _data[actionIndex]; }
		}

		public ProxyResultActionData this[ProxyRequestAction action]
		{
			get { return this[action.ActionIndex]; }
		}
	}

	[Serializable]
	public class ProxyResultActionData
	{
		public readonly Dictionary<string, object> InternalData = new Dictionary<string,object>();
		public object ReturnValue;
		public int ActionIndex;

		internal ProxyResultActionData(int actionIndex)
		{
			ActionIndex = actionIndex;
		}

		public ReturnT GetData<ReturnT>(string key)
		{
			return (ReturnT) InternalData[key];
		}

		public void AddData(string key, object data)
		{
			InternalData[key] = data;
		}
	}
}
