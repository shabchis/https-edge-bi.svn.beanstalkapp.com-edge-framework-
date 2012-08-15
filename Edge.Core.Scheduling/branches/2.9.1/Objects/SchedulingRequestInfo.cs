﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;

namespace Edge.Core.Scheduling.Objects
{
	public class SchedulingRequestInfo
	{
		public Guid RequestID { get; set; }
		public Guid ParentRequestID { get; set; }
		public long LegacyInstanceID { get; set; }
		//public int LegacyParentInstanceID { get; set; }

		public string ServiceName { get; set; }
		public int ProfileID { get; set; }
		public SettingsCollection Options { get; set; }

		public DateTime ScheduledStartTime { get; set; }
		public DateTime ScheduledEndTime { get; set; }
		public DateTime RequestedTime { get; set; }
		public DateTime ActualStartTime { get; set; }
		public DateTime ActualEndTime { get; set; }
		
		public double Progress { get; set; }
		public SchedulingStatus SchedulingStatus { get; set; }
		public ServiceState ServiceState { get; set; }
		public ServiceOutcome ServiceOutcome { get; set; }
		
	}
}
