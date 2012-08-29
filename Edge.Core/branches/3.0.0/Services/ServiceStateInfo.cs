using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Edge.Core.Services
{
	[Serializable]
	public struct ServiceStateInfo
	{
		public ServiceState State;
		public ServiceOutcome Outcome;
		public double Progress;
		public DateTime TimeInitialized;
		public DateTime TimeStarted;
		public DateTime TimeEnded;
		public DateTime TimeLastPaused;
		public DateTime TimeLastResumed;
		public int ResumeCount;

		public ServiceStateInfo Clone()
		{
			return (ServiceStateInfo) this.MemberwiseClone();
		}
	}
}
