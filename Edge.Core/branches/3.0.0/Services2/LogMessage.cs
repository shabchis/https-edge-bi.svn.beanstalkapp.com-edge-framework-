using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Edge.Core.Services2
{
	public class LogMessage
	{
		public string MachineName = Environment.MachineName;
		public int ProcessID = Process.GetCurrentProcess().Id;
		public string Source = null;
		public LogMessageType MessageType = LogMessageType.Information;
		public Guid ServiceHostID = Guid.Empty;
		public Guid ServiceInstanceID = Guid.Empty;
		public int ProfileID = -1;
		public string Message = null;
		public bool IsException = false;
		public string ExceptionDetails = null;
	}

	public enum LogMessageType
	{
		Error = 1,
		Warning = 2,
		Information = 3
	};
}
