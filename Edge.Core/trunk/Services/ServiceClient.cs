using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Edge.Core.Services
{
	/// <summary>
	/// Client for services.
	/// </summary>
	public class ServiceClient<TServiceInterface>: ClientBase<TServiceInterface> where TServiceInterface: class
	{
		#region Constructor
		/*=========================*/

		public ServiceClient()
		{
		}

		public ServiceClient(string endpointConfigurationName, string remoteAddress) :
			base(endpointConfigurationName, remoteAddress)
		{
		}

		/*=========================*/
		#endregion

		#region Properties
		/*=========================*/

		public TServiceInterface Service
		{
			get { return base.Channel; }
		}

		/*=========================*/
		#endregion
	}
}
