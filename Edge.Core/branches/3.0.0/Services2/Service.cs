using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Edge.Core.Services2
{
	public abstract class Service : MarshalByRefObject
	{
		#region Static
		//======================

		internal static ServiceEventType[] SupportedEvents;

		static Service()
		{
			SupportedEvents = new ServiceEventType[]
			{
				ServiceEventType.StateChanged,
				ServiceEventType.OutcomeReported,
				ServiceEventType.ProgressReported,
				ServiceEventType.ChildCreated,
				ServiceEventType.OutputGenerated
			};
		}

		//======================
		#endregion

		#region Control
		//======================

		internal void Start()
		{
		}

		internal protected void Abort()
		{
		}

		//======================
		#endregion

		#region Communication
		//======================

		Dictionary<ServiceEventType, List<ServiceSubscriber>> _subscribers = new Dictionary<ServiceEventType,List<ServiceSubscriber>>();

		internal void Subscribe(ServiceSubscriber subscriber, ServiceEventType events)
		{
			foreach (ServiceEventType eventType in Service.SupportedEvents)
			{
				if ((int)(events & eventType) == 0)
					continue;

				List<ServiceSubscriber> eventSubscribers;
				lock (_subscribers)
				{
					// Get subscribers to this list
					if (!_subscribers.TryGetValue(eventType, out eventSubscribers))
						_subscribers[eventType] = eventSubscribers = new List<ServiceSubscriber>();
				}

				lock (eventSubscribers)
				{
					if (!eventSubscribers.Contains(subscriber))
						eventSubscribers.Add(subscriber);
				}
			}
		}

		internal void Unsubscribe(ServiceSubscriber subscriber, ServiceEventType events)
		{
			foreach (ServiceEventType eventType in Service.SupportedEvents)
			{
				// Check if the event type is supported
				if ((int)(events & eventType) == 0)
					continue;

				List<ServiceSubscriber> eventSubscribers;
				lock (_subscribers)
				{
					if (!_subscribers.TryGetValue(eventType, out eventSubscribers))
						return;
				}

				lock (eventSubscribers)
				{
					int index = eventSubscribers.IndexOf(subscriber);
					if (index >= 0)
						eventSubscribers.RemoveAt(index);
				}
			}
		}

		private void NotifySubscribers(ServiceEventType eventType, object value)
		{
			List<ServiceSubscriber> eventSubscribers;
			
			// Get subscribers to this event type
			lock (_subscribers)
			{
				if (!_subscribers.TryGetValue(eventType, out eventSubscribers))
					return;
			}

			lock (eventSubscribers)
			{
				foreach (ServiceSubscriber subscriber in eventSubscribers)
					subscriber.Notify(eventType, value);
			}
		}
		
		//======================
		#endregion 

		#region Implementation
		//======================

		protected abstract Edge.Core.Services.ServiceOutcome DoWork();

		protected virtual void OnEnded(Edge.Core.Services.ServiceState state)
		{
		}

		//======================
		#endregion

		#region Instance
		//======================

		Services.ServiceState _state;

		protected Edge.Core.Services.ServiceState State
		{
			get { return _state; }
			private set
			{
				_state = value;
				NotifySubscribers(ServiceEventType.StateChanged, _state);
			}
		}

		//======================
		#endregion

		#region Logging
		//======================

		protected void Log(LogMessage message)
		{
			throw new NotImplementedException();
		}

		protected void Log(string message, Exception ex)
		{
			throw new NotImplementedException();
		}

		protected void Log(string message, LogMessageType messageType)
		{
			throw new NotImplementedException();
		}

		//======================
		#endregion
	}


}
