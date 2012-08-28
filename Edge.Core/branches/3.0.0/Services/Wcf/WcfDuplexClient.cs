using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace Edge.Core.Services
{
	internal class WcfDuplexClient<I> : DuplexClientBase<I> where I: class
	{
		public WcfDuplexClient(ServiceConnection connection, string endpointName, string endpointAddress)
			: base(new InstanceContext(connection), endpointName, endpointAddress)
		{
			//this.Endpoint.Contract.Operations[0].Behaviors[0]
		}

		public new I Channel
		{
			get { return base.Channel; }
		}
	}
}
