using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline
{
	[Serializable]
	public class DeliveryConflictException : Exception
	{
		public DeliveryOutput[] ConflictingOutputs { get; set; }

		public DeliveryConflictException() { }
		public DeliveryConflictException(string message) : base(message) { }
		public DeliveryConflictException(string message, Exception inner) : base(message, inner) { }
	}
}
