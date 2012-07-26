using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using Edge.Core.Configuration;
using Edge.Core.Scheduling;
using Edge.Core.Scheduling.Objects;
using Edge.Core.Utilities;
using Legacy = Edge.Core.Services;
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
		PingInfo Ping(Guid guid);

		[OperationContract]
		void Abort(Guid guid);

		[OperationContract]
		void ResetUnended();

		[OperationContract]
		Guid AddUnplannedService(int accountID, string serviceName,  DateTime targetDateTime, Dictionary<string, string> options = null);

		[OperationContract]
		ProfileInfo[] GetSchedulingProfiles();

	}

	public interface ISchedulingHostSubscriber
	{
		

		[OperationContract(IsOneWay = true)]
		void InstancesEvents(List<Edge.Core.Scheduling.Objects.ServiceInstanceInfo> instancesEvents);
	}
}
