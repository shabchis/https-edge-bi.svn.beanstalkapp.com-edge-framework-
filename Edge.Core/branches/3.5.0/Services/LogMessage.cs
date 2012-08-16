using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Edge.Core.Services
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

		public override string ToString()
		{
			var builder = new StringBuilder();
			builder.AppendFormat("MachineName: {0}\n", MachineName);
			builder.AppendFormat("ProcessID: {0}\n", ProcessID);
			builder.AppendFormat("Source: {0}\n", Source);
			builder.AppendFormat("MessageType: {0}\n", MessageType);
			builder.AppendFormat("ServiceHostID: {0}\n", ServiceHostID);
			builder.AppendFormat("ServiceInstanceID: {0}\n", ServiceInstanceID);
			builder.AppendFormat("ProfileID: {0}\n", ProfileID);
			builder.AppendFormat("Message: {0}\n", Message);
			builder.AppendFormat("Exception: {0}\n", Exception);
			return builder.ToString();
		}
	}

	public enum LogMessageType
	{
		Verbose = 0,
		Information = 1,
		Warning = 5,
		Error = 7
	};
}
