using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace Edge.Core.Services
{
	internal class WcfDuplexClient<I> : DuplexClientBase<I> where I: class
	{
		public WcfDuplexClient(ServiceEnvironment environment, ServiceConnection connection, string endpointName, string endpointAddress)
			: base(new InstanceContext(connection), endpointName, endpointAddress)
		{
			foreach (OperationDescription description in this.Endpoint.Contract.Operations)
			{
				DataContractSerializerOperationBehavior dcsOperationBehavior = description.Behaviors.Find<DataContractSerializerOperationBehavior>();

				if (dcsOperationBehavior != null)
				{
					description.Behaviors.Remove(dcsOperationBehavior);
					description.Behaviors.Add(new NetDataContractOperationBehavior(description) { StreamingContextObject = environment });
				}
			}
		}

		public new I Channel
		{
			get { return base.Channel; }
		}
	}

	internal class WcfClient<I> : ClientBase<I> where I : class
	{
		public WcfClient(ServiceEnvironment environment, string endpointName, string endpointAddress)
			: base(endpointName, endpointAddress)
		{
			foreach (OperationDescription description in this.Endpoint.Contract.Operations)
			{
				DataContractSerializerOperationBehavior dcsOperationBehavior = description.Behaviors.Find<DataContractSerializerOperationBehavior>();

				if (dcsOperationBehavior != null)
				{
					description.Behaviors.Remove(dcsOperationBehavior);
					description.Behaviors.Add(new NetDataContractOperationBehavior(description) { StreamingContextObject = environment });
				}
			}
		}

		public new I Channel
		{
			get { return base.Channel; }
		}
	}
}
