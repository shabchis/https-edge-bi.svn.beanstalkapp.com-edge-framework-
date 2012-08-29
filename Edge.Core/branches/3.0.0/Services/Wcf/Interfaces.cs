using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

namespace Edge.Core.Services
{
	[ServiceContract(Name = "ServiceExecutionHost", Namespace = "http://www.edge.bi/contracts", SessionMode = SessionMode.Required, CallbackContract = typeof(IServiceConnection))]
	internal interface IServiceExecutionHost
	{
		string HostName { get; }

		[OperationContract(IsOneWay = true)]
		void InitializeService(ServiceConfiguration config, SchedulingInfo schedulingInfo, Guid instanceID, Guid parentInstanceID, Guid connectionGuid);

		[OperationContract(IsOneWay = true)]
		void StartService(Guid instanceID);

		[OperationContract(IsOneWay = true)]
		void ResumeService(Guid instanceID);

		[OperationContract(IsOneWay = true)]
		void AbortService(Guid instanceID);

		[OperationContract(IsOneWay = true)]
		void NotifyState(Guid instanceID);

		[OperationContract]
		void Connect(Guid instanceID, Guid connectionGuid);

		[OperationContract(IsOneWay = true)]
		void Disconnect(Guid instanceID, Guid connectionGuid);
	}

	/// <summary>
	/// Objects that listens for service events and pushes them to the instance object.
	/// </summary>
	internal interface IServiceConnection : IDisposable
	{
		Guid Guid { get; }
		Guid ServiceInstanceID { get; }

		[OperationContract(IsOneWay = true)]
		void ReceiveState(ServiceStateInfo stateInfo);

		[OperationContract(IsOneWay = true)]
		void ReceiveOutput(object output);
	}
}
