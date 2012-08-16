using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services
{
	[Serializable]
	public class ServiceException : Exception
	{
		public ServiceException() { }
		public ServiceException(string message) : base(message) { }
		public ServiceException(string message, Exception inner) : base(message, inner) { }
		protected ServiceException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
