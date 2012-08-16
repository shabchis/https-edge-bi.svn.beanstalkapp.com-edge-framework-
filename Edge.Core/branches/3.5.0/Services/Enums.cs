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
		Paused = 5,
		Ending = 6,
		Ended = 7,
		
	}

	/// <summary>
	/// 
	/// </summary>
	public enum ServiceOutcome
	{
		Unspecified = 0,

		/// <summary>
		/// Service completed successfully.
		/// </summary>
		Success = 1,
		
		/// <summary>
		/// Service failed to complete successfully.
		/// </summary>
		Failure = 2,

		/// <summary>
		/// Service was aborted during execution.
		/// </summary>
		Aborted = 3,
		
		/// <summary>
		/// Service was canceled before it started.
		/// </summary>
		Canceled = 7,

		/// <summary>
		/// Service reached it's max execution time.
		/// </summary>
		TimedOut = 8
	}
}
