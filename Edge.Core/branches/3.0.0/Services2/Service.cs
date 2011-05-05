using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services2
{
	public class Service : MarshalByRefObject
	{
		object _sync = new object();

		internal void Start()
		{
		}

		internal void Abort()
		{
		}

		protected virtual void DoWork()
		{
		}

		protected virtual void OnEnded()
		{
		}

		protected double Progress
		{
			get;
			set;
		}


		Services.ServiceState _state;
		protected Edge.Core.Services.ServiceState State
		{
			get { return _state; }
			set
			{
				// Check the specified value can be set
				_state = value;
				
			}
		}

		private void NotifyStateChanged()
		{
		}

		protected Edge.Core.Services.ServiceOutcome Outcome
		{
			get;
			set;
		}

		/// <summary>
		/// The
		/// </summary>
		/// <param name="message">The LogMessage.Source must be null.</param>
		protected void Log(LogMessage message)
		{
		}

		protected void Log(string message, Exception ex)
		{
		}
	}
}
