using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Core.Services.Configuration;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Edge.Core.Services
{
	[Serializable]
	public class SchedulingRule: ILockable, ICloneable, ISerializable
	{
		SchedulingScope _scope;
		DateTime _specificDateTime;
		TimeSpan _maxDeviationAfter = TimeSpan.FromHours(3);
		TimeSpan _maxDeviationBefore;
		int[] _days = null;
		TimeSpan[] _times = null;

		public SchedulingScope Scope { get { return _scope; } set { _lock.Ensure(); _scope = value; } }
		public DateTime SpecificDateTime { get { return _specificDateTime; } set { _lock.Ensure(); _specificDateTime = value; } }
		public TimeSpan MaxDeviationAfter { get { return _maxDeviationAfter; } set { _lock.Ensure(); _maxDeviationAfter = value; } }
		public TimeSpan MaxDeviationBefore { get { return _maxDeviationBefore; } set { _lock.Ensure(); _maxDeviationBefore = value; } }
		public int[] Days { get { return _days; } set { _lock.Ensure(); _days = value; } }
		public TimeSpan[] Times { get { return _times; } set { _lock.Ensure(); _times = value; } }

		#region Constructors
		//=================

		public SchedulingRule()
		{
		}

		public SchedulingRule(
			SchedulingScope scope,
			DateTime specificDateTime = default(DateTime),
			TimeSpan maxDeviationAfter = default(TimeSpan),
			TimeSpan maxDeviationBefore = default(TimeSpan),
			int[] days = null,
			TimeSpan[] times = null
			)
		{
			_scope = scope;
			_specificDateTime = specificDateTime;
			if (maxDeviationAfter != default(TimeSpan)) _maxDeviationAfter = maxDeviationAfter;
			if (maxDeviationBefore != default(TimeSpan)) _maxDeviationBefore = maxDeviationBefore;
			_days = days;
			_times = times;
		}

		//=================
		#endregion

		#region ILockable Members
		//=================

		[NonSerialized] Padlock _lock = new Padlock();
		[DebuggerNonUserCode] void ILockable.Lock() { ((ILockable)this).Lock(null); }
		[DebuggerNonUserCode] void ILockable.Lock(object key) { _lock.Lock(key); }
		[DebuggerNonUserCode] void ILockable.Unlock(object key) { _lock.Unlock(key); }
		
		public bool IsLocked { get { return _lock.IsLocked; } }

		//=================
		#endregion

		#region ICloneable Members
		//=================

		public SchedulingRule Clone()
		{
			var cloned = new SchedulingRule()
			{
				_scope = _scope,
				_specificDateTime = _specificDateTime,
				_maxDeviationAfter = _maxDeviationAfter,
				_maxDeviationBefore = _maxDeviationBefore,
				_days = _days == null ? null : (int[])_days.Clone(),
				_times = _times == null ? null : (TimeSpan[])_times.Clone()
			};
			return cloned;
		}

		object ICloneable.Clone()
		{
			return this.Clone();
		}

		//=================
		#endregion

		#region ISerializable Members
		//=================

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("_scope", _scope);
			info.AddValue("_specificDateTime", _specificDateTime);
			info.AddValue("_maxDeviationAfter", _maxDeviationAfter);
			info.AddValue("_maxDeviationBefore", _maxDeviationBefore);
			info.AddValue("_days", _days);
			info.AddValue("_times", _times);

			info.AddValue("IsLocked", IsLocked);
		}

		private SchedulingRule(SerializationInfo info, StreamingContext context)
		{
			_scope = (SchedulingScope)info.GetValue("_scope", typeof(SchedulingScope));
			_specificDateTime = info.GetDateTime("_specificDateTime");
			_maxDeviationAfter = (TimeSpan)info.GetValue("_maxDeviationAfter", typeof(TimeSpan));
			_maxDeviationBefore = (TimeSpan)info.GetValue("_maxDeviationBefore", typeof(TimeSpan));
			_days = (int[])info.GetValue("_days", typeof(int[]));
			_times = (TimeSpan[])info.GetValue("_times", typeof(TimeSpan[]));

			if (info.GetBoolean("IsLocked"))
				((ILockable)this).Lock();
		}

		//=================
		#endregion
	}

	public enum SchedulingScope
	{
		Day,
		Week,
		Month,
		Unplanned
	}
}
