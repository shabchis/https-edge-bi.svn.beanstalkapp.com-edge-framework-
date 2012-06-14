using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

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
		public MappingConfigurationException(string message, XmlReader reader) : this(message, null, reader, null) { }
		public MappingConfigurationException(string message, string tagName) : this(message, tagName, null, null) { }
		public MappingConfigurationException(string message, string tagName, XmlReader reader) : this(message, tagName, reader, null) { }
		public MappingConfigurationException(string message, string tagName, XmlReader reader, Exception inner) :
			base(String.Format("{0}{1}{2}",
			tagName == null ? null : String.Format("<{0}>: ", tagName),
			message,
			reader is XmlTextReader ?
				String.Format(" (line: {0}, position: {1})", ((XmlTextReader)reader).LineNumber, ((XmlTextReader)reader).LinePosition) :
				null
			),
			inner)
		{ }
		public MappingConfigurationException(string message) : base(message) { }
		public MappingConfigurationException(string message, Exception inner) : base(message, inner) { }
		protected MappingConfigurationException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
