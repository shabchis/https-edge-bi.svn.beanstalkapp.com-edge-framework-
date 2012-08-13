using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
{
	public enum DeliveryFileStatus
	{
		Empty = 0,
		Retrieved = 1
	}

	public enum DeliveryOutputProcessingState
	{
		Idle = 0,
		Processing = 1
	}

	public enum DeliveryOutputStatus
	{
		// This 
		Empty		= 0,
		Imported	= 1,
		Transformed = 2,
		Staged		= 3,
		Committed	= 4,
		RolledBack	= 5,
		Canceled	= 6,
		PendingRollBack=7
	}
}
