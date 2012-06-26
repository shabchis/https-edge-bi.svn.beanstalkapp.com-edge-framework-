using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using Edge.Core.Scheduling;
using Edge.Core.Scheduling.Objects;
using Legacy = Edge.Core.Services;
using Edge.Core.Utilities;
using System.Data.SqlClient;
using Edge.Core.Configuration;
using Edge.Core.Services;
using System.Threading;

namespace Edge.Core.Scheduling
{
	[ServiceContract(SessionMode = SessionMode.Required, CallbackContract = typeof(ISchedulingHostSubscriber))]
	public interface ISchedulingHost
	{
		[OperationContract]
		void Subscribe();

		[OperationContract]
		Legacy.IsAlive IsAlive(Guid guid);

		[OperationContract]
		void Abort(Guid guid);

		[OperationContract]
		void ResetUnEnded();

		[OperationContract]
		Guid AddUnplanedService(int accountID, string serviceName, Dictionary<string, string> options, DateTime targetDateTime);

		[OperationContract]
		List<AccountServiceInformation> GetServicesConfigurations();
	}

	public interface ISchedulingHostSubscriber
	{
		[OperationContract(IsOneWay = true)]
		void ScheduleCreated(Edge.Core.Scheduling.Objects.ServiceInstanceInfo[] scheduleAndStateInfo);

		[OperationContract(IsOneWay = true)]
		void InstanceEvent(Edge.Core.Scheduling.Objects.ServiceInstanceInfo StateOutcomerInfo);
	}
}
