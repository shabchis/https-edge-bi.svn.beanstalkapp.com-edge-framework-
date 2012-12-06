using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services.Workflow
{
	public class WorkflowService: Service
	{
		public WorkflowNodeInstance WorkflowInstance;

		public new WorkflowServiceConfiguration Configuration
		{
			get { return (WorkflowServiceConfiguration)base.Configuration; }
		}

		protected override ServiceOutcome DoWork()
		{
			if (this.IsFirstRun)
				this.WorkflowInstance = WorkflowNodeInstance.FromConfiguration(this.Configuration);

			var completedSteps = new List<bool>();
			bool complete = ProcessWorkflow(this.WorkflowInstance, WorkflowNodeFailureBehavior.Terminate, completedSteps);

			Progress = completedSteps.Count(b => b) / completedSteps.Count;
			return complete ? ServiceOutcome.Success : ServiceOutcome.Unspecified;
		}

		bool ProcessWorkflow(WorkflowNodeInstance nodeInstance, WorkflowNodeFailureBehavior failureBehavior, List<bool> completedSteps)
		{
			bool complete;

			if (nodeInstance.Node is WorkflowNodeGroup)
			{
				var group = (WorkflowNodeGroup)nodeInstance.Node;
				complete = true;

				foreach (WorkflowNodeInstance child in nodeInstance.Children)
				{
					var stepComplete = ProcessWorkflow(child, group.FailureBehavior, completedSteps);
					complete &= stepComplete;
					if (!stepComplete && group.Mode == WorkflowNodeGroupMode.Linear)
						break;
				}
			}
			else if (nodeInstance.Node is WorkflowStep)
			{
				var step = (WorkflowStep)nodeInstance.Node;
				if (nodeInstance.Instance == null)
				{
					ServiceConfiguration config;
					if(String.IsNullOrEmpty(step.Name)) 
					{
						config = step.ServiceConfiguration;
					}
					else
					{
						config = step.ServiceConfiguration.Derive();
						config.ServiceName = step.Name;
					}
					nodeInstance.Instance = this.NewChildService(config);
					nodeInstance.Instance.StateChanged += new EventHandler(Instance_StateChanged);
					nodeInstance.Instance.Connect();
					try
					{
						Environment.AddToSchedule(nodeInstance.Instance);
					}
					catch (Exception ex)
					{
						throw new WorkflowException(String.Format("Error trying to run the step '{0}'.", nodeInstance.Node.Name), ex);
					}
				}

				if (failureBehavior == WorkflowNodeFailureBehavior.Terminate && nodeInstance.Instance.State == ServiceState.Ended && nodeInstance.Instance.Outcome != ServiceOutcome.Success)
					throw new WorkflowException(String.Format("Workflow step '{0}' failed, terminating.", nodeInstance.Instance.Configuration.ServiceName));

				complete = nodeInstance.Instance.State == ServiceState.Ended;
				completedSteps.Add(complete);
			}
			else
				throw new NotSupportedException(String.Format("Workflow node type '{0}' not supported.", nodeInstance.Node.GetType()));

			return complete;
		}

		void Instance_StateChanged(object sender, EventArgs e)
		{
			var childInstance = (ServiceInstance)sender;
			if (childInstance.State != ServiceState.Ended)
				return;

			// Resume through the host and not through this.Resume() in order to synchronize calls -
			//		important because more than one child could change state at the same time
			((IServiceExecutionHost)this.Host).ResumeService(this.InstanceID);
		}
	}

	[Serializable]
	public class WorkflowException : Exception
	{
		public WorkflowException() { }
		public WorkflowException(string message) : base(message) { }
		public WorkflowException(string message, Exception inner) : base(message, inner) { }
		protected WorkflowException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
