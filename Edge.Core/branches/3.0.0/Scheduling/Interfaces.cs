using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using Edge.Core.Services;

namespace Edge.Core.Scheduling
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
		Guid AddUnplannedService(ServiceConfiguration serviceConfiguration, SchedulingRule rule);

		[OperationContract]
		ProfilesCollection GetSchedulingProfiles();

	}

	public interface ISchedulingHostSubscriber
	{


		[OperationContract(IsOneWay = true)]
		void InstancesEvents(List<Edge.Core.Services.ServiceInstance> ServiceInstances);
	}
}
