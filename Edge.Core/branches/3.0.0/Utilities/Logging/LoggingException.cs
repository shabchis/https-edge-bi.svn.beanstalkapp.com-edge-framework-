using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Utilities
{
	[Serializable]
	public class LoggingException : Exception
	{
		public LoggingException() { }
		public LoggingException(string message) : base(message) { }
		public LoggingException(string message, Exception inner) : base(message, inner) { }
		protected LoggingException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}