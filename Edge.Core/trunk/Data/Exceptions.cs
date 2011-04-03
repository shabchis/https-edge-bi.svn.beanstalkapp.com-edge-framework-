using System;
using System.Data;

namespace Edge.Core.Data
{
	public class DataItemCollectionException: DataException
	{
		public DataItemCollectionException() : base() {}
		public DataItemCollectionException(string message): base(message) {}
		public DataItemCollectionException(string message, Exception innerException): base(message, innerException) {}
	}

	public class DataItemInconsistencyException: DataException
	{
		public DataItemInconsistencyException() : base() {}
		public DataItemInconsistencyException(string message): base(message) {}
		public DataItemInconsistencyException(string message, Exception innerException): base(message, innerException) {}
	}

	public class ConfigurationException: Exception
	{
		public ConfigurationException() : base() {}
		public ConfigurationException(string message): base(message) {}
		public ConfigurationException(string message, Exception innerException): base(message, innerException) {}
	}
}
