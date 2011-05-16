using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Edge.Core.Services2
{
	[Serializable]
	public class LogMessage
	{
		public readonly string MachineName = Environment.MachineName;
		public readonly int ProcessID = Process.GetCurrentProcess().Id;
		public string Source = null;
		public LogMessageType MessageType = LogMessageType.Information;
		public Guid ServiceHostID = Guid.Empty;
		public Guid ServiceInstanceID = Guid.Empty;
		public int ProfileID = -1;
		public string Message = null;
		public Exception Exception = null;

		public bool IsException { get { return Exception != null; } }
	}

	public enum LogMessageType
	{
		Verbose = 0,
		Information = 1,
		Warning = 5,
		Error = 7
	};
}
