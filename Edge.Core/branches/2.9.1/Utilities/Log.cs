using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.ComponentModel;
using Edge.Core.Configuration;
using System.Diagnostics.Eventing.Reader;
using Edge.Core.Services;
using System.Data.SqlClient;
using Edge.Core.Data;

/*
 * The Utilities Namespace is used for various utility classes such as logging and other functions
 *
 */
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

	public enum LogMessageType
	{
		Error = 1,
		Warning = 2,
		Information = 3,
		Debug = 4
	};

	internal class LogEntry
	{
		public string MachineName = Environment.MachineName;
		public int ProcessID = Process.GetCurrentProcess().Id;
		public string Source = null;
		public LogMessageType MessageType = LogMessageType.Information;
		public long ServiceInstanceID = -1;
		public int AccountID = -1;
		public string Message = null;
		public bool IsException = false;
		public string ExceptionDetails = null;
	}



	/// <summary>
	/// A class which writes events into the windows event viewer.
	/// </summary>
	public static class Log
	{
		private static string _source;
		private static IServiceInstance _instance;
		private static Queue<LogEntry> _logQueue = new Queue<LogEntry>();
		private static log4net.ILog logg = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		private static IAsyncResult _asyncResult;
		private static Action _save;
		private static bool _stopThread;

		static Log()
		{
			if (Service.Current != null)
			{
				_instance = Service.Current.Instance;
				_source = _instance.Configuration.Name;
			}
		}

		internal static void InternalWrite(string source, string message, Exception ex, LogMessageType messageType, int accountID = -1)
		{
			LogEntry entry = new LogEntry();
			entry.Source = source;
			entry.MessageType = messageType;
			entry.Message = message;

			if (_instance != null)
				entry.ServiceInstanceID = _instance.InstanceID;

			entry.AccountID = accountID != -1 || _instance == null ? accountID : _instance.AccountID;

			if (ex != null)
			{
				entry.IsException = true;
				entry.ExceptionDetails = ex.ToString();
			}

			lock (_logQueue)
			{
				_logQueue.Enqueue(entry);
			}

			StartPump();
		}

		public static void StartPump()
		{
			if (_asyncResult == null)
			{
				_save = new Action(Pump);
				_asyncResult = _save.BeginInvoke(null, null);
			}
		}

		public static void StopPump()
		{
			_stopThread = true;
			if (_asyncResult != null)
			{
				_asyncResult.AsyncWaitHandle.WaitOne();
			}
		}

		#region Public write methods
		// ---------------------------------
		public static void Write(string message, Exception ex, LogMessageType messageType, int accountID = -1)
		{

			if (Service.Current == null)
				throw new InvalidOperationException("Source parameter must be specified when writing to the log outside of a service context.");

			InternalWrite(_source, message, ex, messageType, accountID);
		}

		public static void Write(string message, LogMessageType messageType)
		{
			Write(message, (Exception)null, messageType);
		}

		public static void Write(string message, Exception ex)
		{
			Write(message, ex, LogMessageType.Error);
		}

		public static void Write(string source, string message, Exception ex, LogMessageType messageType)
		{
			// Use source-less version when source is null
			if (String.IsNullOrEmpty(source))
				Write(message, ex, messageType);

			if (Service.Current != null)
				throw new InvalidOperationException("Cannot specify source when writing to the log within a service context.");

			InternalWrite(source, message, ex, messageType);
		}

		public static void Write(string source, string message, LogMessageType messageType)
		{
			Write(source, message, null, messageType);
		}
		
		public static void Write(string source, string message, Exception ex)
		{
			Write(source, message, ex, LogMessageType.Error);
		}
		// ---------------------------------
		#endregion

		public static void Pump()
		{
			while (_stopThread!=true)
			{
				while (_logQueue.Count > 0)
				{
					WriteToDb();
				}
				Thread.Sleep(100);
			}
			while (_logQueue.Count > 0)
				WriteToDb();
		}

		private static void WriteToDb()
		{
			LogEntry entry;
			lock (_logQueue)
			{
				entry = _logQueue.Dequeue();
			}
			log4net.ThreadContext.Properties["@dateRecorded"] = DateTime.Now;
			log4net.ThreadContext.Properties["@machineName"] = entry.MachineName;
			log4net.ThreadContext.Properties["@processID"] = entry.ProcessID;
			log4net.ThreadContext.Properties["@source"] = entry.Source;
			log4net.ThreadContext.Properties["@messageType"] = (int)entry.MessageType;
			log4net.ThreadContext.Properties["@serviceInstanceID"] = entry.ServiceInstanceID;
			log4net.ThreadContext.Properties["@accountID"] = entry.AccountID;
			log4net.ThreadContext.Properties["@message"] = entry.Message;
			log4net.ThreadContext.Properties["@isException"] = entry.IsException;
			log4net.ThreadContext.Properties["@exceptionDetails"] = entry.ExceptionDetails;

			switch (entry.MessageType)
			{
				case LogMessageType.Error:
					logg.Fatal(string.Empty);
					break;
				case LogMessageType.Warning:
					logg.Error(string.Empty);
					break;
				case LogMessageType.Information:
					logg.Info(string.Empty);
					break;
				case LogMessageType.Debug:
					logg.Debug(string.Empty);
					break;

			}
		}
	}
}
