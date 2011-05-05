using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;

namespace Edge.Core.Services2
{
	public class ServiceInstance
	{
		internal Service ServiceRef { get; set; }

		public Guid InstanceID { get; internal set; }
		public ServiceConfiguration Configuration;
		public ServiceExecutionContext Context;
		public ServiceInstance ParentInstance;
		public double Progress;
		public Edge.Core.Services.ServiceState State;
		public Edge.Core.Services.ServiceOutcome Outcome;
		public object Result;
		public SchedulingData SchedulingData;
		public ReadOnlyObservableCollection<ServiceInstance> ChildInstances;

		public event EventHandler StateChanged;
		public event EventHandler OutcomeReported;
		public event EventHandler ProgressReported;
		public event EventHandler LogMessageGenerated;

		public void Start()
		{
			var action = new Action(this.ServiceRef.Start);
			action.BeginInvoke(null, null);
		}

		public void Abort()
		{
			var action = new Action(this.ServiceRef.Abort);
			action.BeginInvoke(null, null);
		}

		public override string ToString()
		{
			return String.Format("{0} (profile: {1}, guid: {2})",
				Configuration.ServiceName,
				Configuration.Profile == null ? "default" : Configuration.Profile.Name,
				InstanceID
			);
		}
	}

	public class ServiceCreatedEventArgs : EventArgs
	{
		public ServiceInstance Instance
		{
			get;
			set;
		}
	}
}
