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
        public event EventHandler<ServiceScheduleRequestedEventArgs> ServiceScheduleRequested;
        public ServiceEnvironment Environment { get; private set; }
		public ServiceEnvironmentEventType[] EventTypes { get; private set; }
		public Guid ListenerID { get; private set; }

		internal WcfHost WcfHost { get; private set; }

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

        void IServiceEnvironmentEventListener.ServiceScheduleRequestedEvent(ServiceScheduleRequestedEventArgs args)
        {
            if (ServiceScheduleRequested != null)
                ServiceScheduleRequested(this, args);
        }

		public void Close()
		{
			((IDisposable)this).Dispose();
		}

		void IDisposable.Dispose()
		{
			this.Environment.UnregisterEventListener(this);

			// Close WCF host				
			if (this.WcfHost != null)
			{
				if (this.WcfHost.State == CommunicationState.Faulted)
					this.WcfHost.Abort();
				else
					this.WcfHost.Close();
			}
		}
	}

    [ServiceContract(Name = "ServiceEnvironmentEventListener", Namespace = "http://www.edge.bi/contracts")]
    internal interface IServiceEnvironmentEventListener
    {
        [OperationContract(IsOneWay = true)]
        void ServiceScheduleRequestedEvent(ServiceScheduleRequestedEventArgs args);
    }
}
