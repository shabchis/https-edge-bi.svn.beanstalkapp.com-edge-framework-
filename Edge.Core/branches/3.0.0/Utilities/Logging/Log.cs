using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Edge.Core.Services;
using System.Threading;


namespace Edge.Core.Utilities
{	
	/// <summary>
	/// A class which writes events into the windows event viewer.
	/// </summary>
	public static class Log
	{
		private static Queue<LogMessage> _logQueue = new Queue<LogMessage>();
		private static log4net.ILog _log = log4net.LogManager.GetLogger(typeof(Log));
		private static IAsyncResult _asyncResult;
		private static Action _save;
		private static volatile bool _stopThread;

		#region Control methods
		// ======================================

		public static void Start()
		{
			if (_asyncResult != null)
				throw new InvalidOperationException("Log is already started.");
			
			_save = new Action(Pump);
			_asyncResult = _save.BeginInvoke(null, null);			
		}

		public static void Stop()
		{
			if (_asyncResult == null)
				return;

			_stopThread = true;
			_asyncResult.AsyncWaitHandle.WaitOne();

			try { _save.EndInvoke(_asyncResult); }
			catch (Exception ex)
			{
				Submit(new LogMessage(typeof(Log).FullName, null, "Exception discovered while stopping the log queue pump.", ex, LogMessageType.Warning));
			}
		}

		// ======================================
		#endregion

		#region Write methods
		// ======================================

		public static void Write(string source, string message, LogMessageType messageType)
		{
			Write(new LogMessage(source, null, message, null, messageType));
		}

		public static void Write(string source, string contextInfo, string message, LogMessageType messageType)
		{
			Write(new LogMessage(source, contextInfo, message, null, messageType));
		}

		public static void Write(string source, string message, Exception ex, LogMessageType messageType = LogMessageType.Error)
		{
			Write(new LogMessage(source, null, message, ex, messageType));
		}

		public static void Write(string source, string contextInfo, string message, Exception ex, LogMessageType messageType = LogMessageType.Error)
		{
			Write(new LogMessage(source, contextInfo, message, ex, messageType));
		}

		internal static void Write(LogMessage message)
		{
			if (Service.Current != null)
				throw new InvalidOperationException("Use Service.Log to write messages from within a service.");

			if (_asyncResult == null)
				throw new InvalidOperationException("Cannot write to log because Log.Start hasn't been called.");

			lock (_logQueue)
			{
				_logQueue.Enqueue(message);
			}
		}

		// ======================================
		#endregion

		#region Heavy lifting
		// ======================================

		static void Pump()
		{
			while (_stopThread != true)
			{
				FlushQueue();
				Thread.Sleep(100);
			}
			FlushQueue();
		}

		static void FlushQueue()
		{
			while (_logQueue.Count > 0)
			{
				LogMessage entry;
				lock (_logQueue) { entry = _logQueue.Dequeue(); }
				Submit(entry);
			}
		}

		static void Submit(LogMessage entry)
		{
			log4net.ThreadContext.Properties["@dateRecorded"] = DateTime.Now;
			log4net.ThreadContext.Properties["@machineName"] = entry.MachineName;
			log4net.ThreadContext.Properties["@processID"] = entry.ProcessID;
			log4net.ThreadContext.Properties["@source"] = entry.Source;
			log4net.ThreadContext.Properties["@contextInfo"] = entry.ContextInfo;
			log4net.ThreadContext.Properties["@messageType"] = (int)entry.MessageType;
			log4net.ThreadContext.Properties["@message"] = entry.Message;
			log4net.ThreadContext.Properties["@serviceInstanceID"] = entry.ServiceInstanceID.ToString("N");
			log4net.ThreadContext.Properties["@serviceProfileID"] = entry.ServiceProfileID.ToString("N");
			log4net.ThreadContext.Properties["@isException"] = entry.IsException;
			log4net.ThreadContext.Properties["@exceptionDetails"] = entry.ExceptionDetails;

			switch (entry.MessageType)
			{
				case LogMessageType.Error:
					_log.Fatal(string.Empty);
					break;
				case LogMessageType.Warning:
					_log.Error(string.Empty);
					break;
				case LogMessageType.Information:
					_log.Info(string.Empty);
					break;
				case LogMessageType.Debug:
					_log.Debug(string.Empty);
					break;

			}
		}

		// ======================================
		#endregion
	}
	 
}
