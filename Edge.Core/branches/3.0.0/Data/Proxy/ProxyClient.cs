using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Data;
using System.ServiceModel;

namespace Edge.Core.Data.Proxy
{
	public class ProxyClient: IDisposable
	{
		#region Static
		/*=========================*/
		static Dictionary<Thread, ProxyClient> _instances = new Dictionary<Thread, ProxyClient>();
		//static Type _proxyType = null;

		/// <summary>
		/// 
		/// </summary>
		internal static ProxyClient Current
		{
			get
			{
				//if (_proxyType == null)
				//	throw new InvalidOperationException("ProxyClientClass must be set before the proxy client can be used");

				ProxyClient current = null;
				if (!_instances.ContainsKey(Thread.CurrentThread))
				{
					current = new ProxyClient(); //(ProxyClient) _proxyType.GetConstructor(Type.EmptyTypes).Invoke(null);
					current._disposed += new EventHandler(InstanceDisposed);
					_instances.Add(current._thread, current);
				}
				else
					current = _instances[Thread.CurrentThread] as ProxyClient;

				return current;
			}
		}

		private static void InstanceDisposed(object sender, EventArgs e)
		{
			ProxyClient p = sender as ProxyClient;
			if (p != null)
				_instances.Remove(p._thread);
		}

		/*
		public static Type Class
		{
			get
			{
				return _proxyType;
			}
			set
			{
				if (value == null)
					throw new ArgumentNullException();

				if (!value.IsSubclassOf(typeof(ProxyClient)))
					throw new ArgumentException("ProxyClientClass must derive from base class ProxyClient");

				_proxyType = value;
			}
		}
		*/

		public static ProxyRequest Start(bool async)
		{
			return Current.BeginRequest(async);
		}

		public static ProxyRequest Start()
		{
			return Current.BeginRequest(false);
		}

		public static ProxyRequest Request
		{
			get { return Current._request; }
		}

		public static ProxyResult Result
		{
			get { return Current._result; }
		}

		public static Action OnComplete
		{
			set { Current._onComplete = value; }
		}

		/*=========================*/
		#endregion

		#region Instance
		/*=========================*/

		EventHandler _disposed;
		Action _onComplete;
		ProxyRequest _request;
		ProxyResult _result;
		Thread _thread;

		protected ProxyClient()
		{
			_thread = Thread.CurrentThread;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		private ProxyRequest BeginRequest(bool async)
		{
			if (_request != null)
				throw new InvalidOperationException("Request already in progress");

			_request = new ProxyRequest(async);
			_request.Send = new Action(this.SendRequest);
			return _request;
		}

		protected virtual void SendRequest()
		{
			//throw new NotImplementedException("Override SendRequest and implement");
			WSHttpBinding binding = new WSHttpBinding();
			binding.SendTimeout = new TimeSpan(1, 0, 0);
			binding.ReceiveTimeout = new TimeSpan(1, 0, 0);
			EndpointAddress address = new EndpointAddress("http://localhost:3344/ProxyServer.svc");
			ChannelFactory<IProxyServer> channelFactory = new ChannelFactory<IProxyServer>(binding, address);
			IProxyServer service = channelFactory.CreateChannel();
			_result = service.ProcessRequest(_request);
			EndRequest(null);
		}

		protected void EndRequest(IAsyncResult asyncResult)
		{
			for (int i = 0; i < _request.Actions.Count; i++)
			{
				if (_result[i].ReturnValue is Exception && _request.StopOnError)
					throw (_result[i].ReturnValue as Exception);

				//if (_request.Actions[i].Target != null)
				//{
				//    // Auto-restore the object data
				//    //_request.Actions[i].Target.Restore(_result.Data[i] as DataTable);
				//}

				if (_request.Actions[i].OnComplete != null)
					_request.Actions[i].OnComplete();
			}

			if (_onComplete != null)
				_onComplete();

			_request = null;
			_result = null;
		}

		public void Dispose()
		{
			if (_request != null)
				_request.Canceled = true;

			if (_disposed != null)
				_disposed(this, EventArgs.Empty);
		}

		/*=========================*/
		#endregion
	}
}
