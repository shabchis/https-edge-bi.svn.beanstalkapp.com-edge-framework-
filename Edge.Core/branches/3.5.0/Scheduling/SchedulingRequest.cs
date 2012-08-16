using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using Edge.Core.Services.Configuration;
using System.Diagnostics;

namespace Edge.Core.Services
{
	public class SchedulingRequestDISABLED: ILockable
	{
		public Guid RequestID { get; private set; }
		public ServiceInstance Instance { get; private set; }
		public ServiceConfiguration Configuration { get; private set; }
		public SchedulingRule Rule { get; private set; }
		public DateTime RequestedTime { get; private set; }
		public DateTime ScheduledStartTime { get; internal set; }
		public DateTime ScheduledEndTime { get; internal set; }
		public SchedulingStatus SchedulingStatus { get; set; }

		public event EventHandler Rescheduled;
		public event EventHandler Expired;

		internal SchedulingRequestDISABLED()
		{
			this.RequestID = Guid.NewGuid();
		}

		public string Signature
		{
			get
			{
				return String.Format("profile:{0},base:{1},name:{2},scope:{3},time:{4}", this.Configuration.Profile.ID, Configuration.BaseConfiguration.ServiceName, Configuration.ServiceName, Rule.Scope, RequestedTime);
			}
		}

		public TimeSpan ActualDeviation
		{
			get { return this.ScheduledStartTime.Subtract(this.RequestedTime); }
		}


		#region Locking

		[NonSerialized] Padlock _lock = new Padlock();
		public bool IsLocked { get { return _lock.IsLocked; } }
		[DebuggerNonUserCode] void ILockable.Lock(object key) { _lock.Lock(key); }
		[DebuggerNonUserCode] void ILockable.Unlock(object key) { _lock.Unlock(key); }

		#endregion
	}
	
}
