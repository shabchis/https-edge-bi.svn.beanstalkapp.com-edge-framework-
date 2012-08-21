using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Edge.Core.Services
{
	[Serializable]
	public class SchedulingInfo: ILockable, ISerializable
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

		public SchedulingInfo()
		{
		}

		#region ILockable Members
		//=================

		[NonSerialized] Padlock _lock = new Padlock();
		[DebuggerNonUserCode] void ILockable.Lock() { ((ILockable)this).Lock(null); }
		[DebuggerNonUserCode] void ILockable.Lock(object key) { _lock.Lock(key); }
		[DebuggerNonUserCode] void ILockable.Unlock(object key) { _lock.Unlock(key); }
		public bool IsLocked { get { return _lock.IsLocked; } }

		//=================
		#endregion

		#region ISerializable Members
		//=================

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("_requestedTime", _requestedTime);
			info.AddValue("_expectedStartTime", _expectedStartTime);
			info.AddValue("_expectedEndTime", _expectedEndTime);
			info.AddValue("_maxDeviationAfter", _maxDeviationAfter);
			info.AddValue("_maxDeviationBefore", _maxDeviationBefore);
			info.AddValue("_schedulingStatus", _schedulingStatus);
			info.AddValue("_schedulingScope", _schedulingScope);

			info.AddValue("IsLocked", IsLocked);
		}

		private SchedulingInfo(SerializationInfo info, StreamingContext context)
		{
			_requestedTime = info.GetDateTime("_requestedTime");
			_expectedStartTime = info.GetDateTime("_expectedStartTime");
			_expectedEndTime = info.GetDateTime("_expectedEndTime");
			_maxDeviationAfter = (TimeSpan)info.GetValue("_maxDeviationAfter", typeof(TimeSpan));
			_maxDeviationBefore = (TimeSpan)info.GetValue("_maxDeviationBefore", typeof(TimeSpan));
			_schedulingStatus = (SchedulingStatus)info.GetValue("_schedulingStatus", typeof(SchedulingStatus));
			_schedulingScope = (SchedulingScope)info.GetValue("_schedulingScope", typeof(SchedulingScope));

			if (info.GetBoolean("IsLocked"))
				((ILockable)this).Lock();
		}

		//=================
		#endregion
	}
}
