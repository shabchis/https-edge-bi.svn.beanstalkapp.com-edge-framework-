using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;

namespace Edge.Core.Services
{
	internal class WcfDuplexClient<I> : DuplexClientBase<I>
	{
		public WcfDuplexClient(ServiceConnection connection)
			: base(new InstanceContext(connection), )
		{
		}

		public new I Channel
		{
			get { return base.Channel; }
		}
	}
}
