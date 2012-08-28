using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Channels;
using System.ServiceModel.Configuration;
using System.ServiceModel.Description;

namespace Edge.Core.Services
{
	internal class WcfHost : ServiceHost//, IErrorHandler
	{
		ServiceEnvironment _environment;

		public WcfHost(ServiceEnvironment environment, object singletonInstance) : base(singletonInstance)
		{
			_environment = environment;

			foreach (ServiceEndpoint endpoint in this.Description.Endpoints)
			{
				foreach (OperationDescription description in endpoint.Contract.Operations)
				{
					DataContractSerializerOperationBehavior dcsOperationBehavior = description.Behaviors.Find<DataContractSerializerOperationBehavior>();

					if (dcsOperationBehavior != null)
					{
						description.Behaviors.Remove(dcsOperationBehavior);
						description.Behaviors.Add(new NetDataContractOperationBehavior(description) { StreamingContextObject = _environment });
					}
				}
			}
		}

		protected override void InitializeRuntime()
		{
			base.InitializeRuntime();

			// Add the service as its own error handler
			//foreach (ChannelDispatcher channelDispatcher in this.ChannelDispatchers)
			//	channelDispatcher.ErrorHandlers.Add(this as IErrorHandler);	
		}
		/*
		void IErrorHandler.ProvideFault(Exception ex, MessageVersion version, ref Message fault)
		{
		}

		bool IErrorHandler.HandleError(Exception error)
		{
			return true;
		}
		*/
	}
}
