using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Mapping
{
	[Serializable]
	public class MappingException : Exception
	{
		public MappingException() { }
		public MappingException(string message) : base(message) { }
		public MappingException(string message, Exception inner) : base(message, inner) { }
		protected MappingException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

	[Serializable]
	public class MappingConfigurationException : Exception
	{
		public MappingConfigurationException() { }
		public MappingConfigurationException(string message) : base(message) { }
		public MappingConfigurationException(string message, Exception inner) : base(message, inner) { }
		protected MappingConfigurationException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
