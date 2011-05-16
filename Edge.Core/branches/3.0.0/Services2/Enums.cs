﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services2
{
	/// <summary>
	/// 
	/// </summary>
	internal enum ServiceEventType
	{
		StateChanged,
		OutcomeReported,
		ProgressReported,
		ChildCreated,
		OutputGenerated
	}

	public enum ServiceState
	{
		Uninitialized = 0,
		Initializing = 1,
		Ready = 2,
		Starting = 3,
		Running = 4,
		Waiting = 5,
		Ending = 6,
		Ended = 7,
		
	}

	/// <summary>
	/// 
	/// </summary>
	public enum ServiceOutcome
	{
		Unspecified = 0,
		Success = 1,
		Failure = 2,
		Aborted = 3,
		Timeout = 4,
		CouldNotBeScheduled = 5,
		Error = 6
	}

	/// <summary>
	/// 
	/// </summary>
	public enum ServicePriority
	{
		Low = 1,
		Normal = 5,
		High = 9,
		Urgent = 10,
	}
}
