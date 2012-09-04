using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
{
	[Serializable]
	public class DeliveryManagerException : Exception
	{
		public DeliveryManagerException() { }
		public DeliveryManagerException(string message) : base(message) { }
		public DeliveryManagerException(string message, Exception inner) : base(message, inner) { }
		protected DeliveryManagerException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
