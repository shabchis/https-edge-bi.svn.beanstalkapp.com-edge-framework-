using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using System.Diagnostics;

namespace Edge.Core.Services
{
	[Serializable]
	public class SchedulingInfo: ILockable
	{
		DateTime _requestedTime;
		DateTime _expectedStartTime;
		DateTime _expectedEndTime;
		TimeSpan _maxDeviationAfter;
		TimeSpan _maxDeviationBefore;
		SchedulingStatus _schedulingStatus;
		SchedulingScope _schedulingScope;

		public DateTime RequestedTime { get { return _requestedTime; } set { _lock.Ensure(); _requestedTime = value; } }
		public DateTime ExpectedStartTime { get { return _expectedStartTime; } set { _lock.Ensure(); _expectedStartTime = value; } }
		public DateTime ExpectedEndTime { get { return _expectedEndTime; } set { _lock.Ensure(); _expectedEndTime = value; } }
		public TimeSpan MaxDeviationAfter { get { return _maxDeviationAfter; } set { _lock.Ensure(); _maxDeviationAfter = value; } }
		public TimeSpan MaxDeviationBefore { get { return _maxDeviationBefore; } set { _lock.Ensure(); _maxDeviationBefore = value; } }
		public SchedulingStatus SchedulingStatus { get { return _schedulingStatus; } set { _lock.Ensure(); _schedulingStatus = value; } }
		public SchedulingScope SchedulingScope { get { return _schedulingScope; } set { _lock.Ensure(); _schedulingScope = value; } }
		public TimeSpan ExpectedDeviation  { get { return ExpectedStartTime.Subtract(RequestedTime); } }

		#region ILockable Members
		//=================

		[NonSerialized] Padlock _lock = new Padlock();
		public bool IsLocked { get { return _lock.IsLocked; } }
		[DebuggerNonUserCode] void ILockable.Lock() { ((ILockable)this).Lock(new object()); }
		[DebuggerNonUserCode] void ILockable.Lock(object key) { _lock.Lock(key); }
		[DebuggerNonUserCode] void ILockable.Unlock(object key) { _lock.Unlock(key); }

		//=================
		#endregion

	}
}
