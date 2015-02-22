using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;

namespace Edge.Core.Scheduling
{
	[ServiceContract]
	public interface IScheduleManager
	{
		[OperationContract]
		void BuildSchedule();

		[OperationContract]
		bool AddToSchedule(string serviceName, int accountID, DateTime targetTime, SettingsCollection options);
	}
}
