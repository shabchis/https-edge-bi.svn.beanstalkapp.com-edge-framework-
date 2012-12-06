using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;

namespace Edge.Core.Services
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple)]
    public class ServiceEnvironmentEventListener : IServiceEnvironmentEventListener
    {
        WcfHost _eventListener = null;

        public event EventHandler<ServiceScheduleRequestedEventArgs> ServiceScheduleRequested;
        public ServiceEnvironment Environment { get; private set; }

        internal ServiceEnvironmentEventListener(ServiceEnvironment environment)
        {
            this.Environment = environment;

            _eventListener = new WcfHost(environment, this);

            ServiceEndpoint[] rawEndpoints = _eventListener.Description.Endpoints.ToArray();
            _eventListener.Description.Endpoints.Clear();
            foreach (ServiceEndpoint endpoint in rawEndpoints)
            {
                endpoint.Address = new EndpointAddress(new Uri(endpoint.Address.Uri.ToString().Replace("{guid}", Guid.NewGuid().ToString("N"))));
                _eventListener.AddServiceEndpoint(endpoint);
            }

            _eventListener.Open();
        }

        void IServiceEnvironmentEventListener.ServiceScheduleRequestedEvent(ServiceScheduleRequestedEventArgs args)
        {
            if (ServiceScheduleRequested != null)
                ServiceScheduleRequested(this, args);
        }
    }

    [ServiceContract(Name = "ServiceEnvironmentEventListener", Namespace = "http://www.edge.bi/contracts")]
    internal interface IServiceEnvironmentEventListener
    {
        [OperationContract(IsOneWay = true)]
        void ServiceScheduleRequestedEvent(ServiceScheduleRequestedEventArgs args);
    }
}
