using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Queries
{
	[Serializable]
	public class QueryTemplateException : Exception
	{
		public QueryTemplateException() { }
		public QueryTemplateException(string message) : base(message) { }
		public QueryTemplateException(string message, Exception inner) : base(message, inner) { }
		protected QueryTemplateException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}

	[Serializable]
	public class QueryExecutionException : Exception
	{
		public QueryExecutionException() { }
		public QueryExecutionException(string message) : base(message) { }
		public QueryExecutionException(string message, Exception inner) : base(message, inner) { }
		protected QueryExecutionException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
