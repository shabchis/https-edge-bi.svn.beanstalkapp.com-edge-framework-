using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract partial class ChannelSpecificObject : EdgeObject
	{
		public Channel Channel;
		public string OriginalID;
		public ObjectStatus Status;
	}

	public enum ObjectStatus
	{
		Unknown = 0,
		Active = 1,
		Paused = 2,
		Suspended = 3,
		Ended = 4,
		Deleted = 5,
		Pending = 6,
		Duplicate = 999
	}

}
