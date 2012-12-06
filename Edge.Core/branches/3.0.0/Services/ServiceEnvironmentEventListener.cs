using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;

namespace Edge.Core.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ServiceEnvironmentEventListener : IServiceEnvironmentEventListener, IDisposable
    {
        public ServiceEnvironment Environment { get; private set; }
		public ServiceEnvironmentEventType[] EventTypes { get; private set; }
		public Guid ListenerID { get; private set; }

		internal WcfHost WcfHost { get; private set; }

		private event EventHandler<ServiceInstanceEventArgs> _serviceRequiresScheduling;
		private event EventHandler<ScheduleUpdatedEventArgs> _scheduleUpdated;

		#region WCF open/close
		// ------------------------------

		internal ServiceEnvironmentEventListener(ServiceEnvironment environment, ServiceEnvironmentEventType[] eventTypes)
        {
			this.ListenerID = Guid.NewGuid();
            this.Environment = environment;
			this.EventTypes = eventTypes;
			this.WcfHost = new WcfHost(environment, this);

            ServiceEndpoint[] rawEndpoints = WcfHost.Description.Endpoints.ToArray();
            WcfHost.Description.Endpoints.Clear();
            foreach (ServiceEndpoint endpoint in rawEndpoints)
            {
				endpoint.Address = new EndpointAddress(new Uri(endpoint.Address.Uri.ToString().Replace("{guid}", this.ListenerID.ToString("N"))));
                WcfHost.AddServiceEndpoint(endpoint);
            }

            WcfHost.Open();
        }

		public void Close()
		{
			this.Close(true);
		}
 
		internal void Close(bool unregister)
		{
			if (unregister)
				this.Environment.UnregisterEventListener(this);

			// Close WCF host				
			if (this.WcfHost != null)
			{
				if (this.WcfHost.State == CommunicationState.Opened)
					this.WcfHost.Close();
				else if (this.WcfHost.State != CommunicationState.Closed)
					this.WcfHost.Abort();
			}
		}

		void IDisposable.Dispose()
		{
			this.Close();
		}

		// ------------------------------
		#endregion

		#region Event wiring
		// ------------------------------

		void Ensure(ServiceEnvironmentEventType eventType)
		{
			if (!EventTypes.Contains(eventType))
				throw new InvalidOperationException(String.Format("Cannot connect listener to event {0} because it is not registered to receive this event.", eventType));
		}

		public event EventHandler<ServiceInstanceEventArgs> ServiceRequiresScheduling
		{
			add { Ensure(ServiceEnvironmentEventType.ServiceRequiresScheduling); _serviceRequiresScheduling += value; }
			remove { _serviceRequiresScheduling -= value; }
		}

		void IServiceEnvironmentEventListener.ServiceRequiresScheduling(ServiceInstanceEventArgs args)
		{
			if (_serviceRequiresScheduling != null)
				_serviceRequiresScheduling(this, args);
		}

		public event EventHandler<ScheduleUpdatedEventArgs> ScheduleUpdated
		{
			add { Ensure(ServiceEnvironmentEventType.ScheduleUpdated); _scheduleUpdated += value; }
			remove { _scheduleUpdated -= value; }
		}

		void IServiceEnvironmentEventListener.ScheduleUpdated(ScheduleUpdatedEventArgs args)
		{
			if (_scheduleUpdated != null)
				_scheduleUpdated(this, args);
		}

		// ------------------------------
		#endregion
	}


    [ServiceContract(Name = "ServiceEnvironmentEventListener", Namespace = "http://www.edge.bi/contracts")]
    public interface IServiceEnvironmentEventListener
    {
        [OperationContract(IsOneWay = true)]
        void ServiceRequiresScheduling(ServiceInstanceEventArgs args);

		[OperationContract(IsOneWay = true)]
		void ScheduleUpdated(ScheduleUpdatedEventArgs args);
    }

	
}
