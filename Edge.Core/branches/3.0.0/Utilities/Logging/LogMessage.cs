using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Edge.Core.Utilities
{
	internal class LogMessage
	{
		public string MachineName = Environment.MachineName;
		public int ProcessID = Process.GetCurrentProcess().Id;
		public LogMessageType MessageType = LogMessageType.Information;
		public string Source = null;
		public string ContextInfo = null;
		public string Message = null;
		public bool IsException = false;
		public string ExceptionDetails = null;
		public Guid ServiceInstanceID = Guid.Empty;
		public Guid ServiceProfileID = Guid.Empty;

		public LogMessage(string source, string contextInfo, string message, Exception ex, LogMessageType messageType)
		{
			if (String.IsNullOrEmpty(source))
				throw new ArgumentException("Source parameter must be specified when writing to the log.", "source");

			this.Source = source;
			this.ContextInfo = contextInfo;
			this.MessageType = messageType;
			this.Message = message;

			if (ex != null)
			{
				this.IsException = true;
				this.ExceptionDetails = ex.ToString();
			}
		}
	}
}
