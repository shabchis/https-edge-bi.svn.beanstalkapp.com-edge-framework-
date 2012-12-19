using System;
using System.Collections.Generic;
using System.ServiceModel;

namespace Edge.Core.Services.Scheduling
{
	[ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(ISchedulingHostSubscriber))]
	public interface ISchedulingHost
	{
		[OperationContract]
		void Subscribe();

		[OperationContract]
		void Unsubscribe();

		[OperationContract]
		void Abort(Guid guid);

		[OperationContract]
		void ResetUnended();

		[OperationContract]
		[NetDataContract]
		Guid AddUnplannedService(ServiceConfiguration serviceConfiguration);

		[OperationContract]
		[NetDataContract]
		ServiceProfile[] GetSchedulingProfiles();

	}

	public interface ISchedulingHostSubscriber
	{
		[OperationContract(IsOneWay = true)]
		[NetDataContract]
		void InstancesEvents(List<ServiceInstance> serviceInstances);
	}
}
