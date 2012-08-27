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
	internal class WcfHost : ServiceHost, IErrorHandler
	{
		public WcfHost(object singletonInstance) : base(singletonInstance)
		{
		}

		protected override void InitializeRuntime()
		{
			base.InitializeRuntime();

			// Add the service as its own error handler
			foreach (ChannelDispatcher channelDispatcher in this.ChannelDispatchers)
				channelDispatcher.ErrorHandlers.Add(this as IErrorHandler);
		}

		void IErrorHandler.ProvideFault(Exception ex, MessageVersion version, ref Message fault)
		{
		}

		bool IErrorHandler.HandleError(Exception error)
		{
			return true;
		}

	}
}
