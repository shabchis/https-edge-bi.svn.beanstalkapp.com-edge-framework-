using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
{
	/// <summary>
	/// 
	/// </summary>
	internal enum ServiceEventType
	{
		OutcomeReported,
		StateChanged,
		ChildServiceRequested,
		ProgressReported
	}

	/// <summary>
	/// 
	/// </summary>
	public enum ServiceState
	{
		Uninitialized = 0,
		Initializing = 1,
		Ready = 2,
		Starting = 3,
		Running = 4,
		Waiting = 5,
		Ended = 6,
		Aborting = 7
	}

	/// <summary>
	/// 
	/// </summary>
	public enum ServiceOutcome
	{
		Unspecified = 0,
		Success = 1,
		Failure = 2,
		Aborted = 3
	}

	public enum ServicePriority
	{
		Low = 0,
		Normal = 1,
		High = 2,
		Immediate = 3,
	}
}
