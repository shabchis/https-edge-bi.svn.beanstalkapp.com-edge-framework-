﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{

	public enum ObjectStatus
	{
		Unknown = -1,
		Pending = 0,
		Active = 1,
		Paused = 2,
		Suspended = 3,
		Ended = 4,
		Deleted = 5
	}
}
