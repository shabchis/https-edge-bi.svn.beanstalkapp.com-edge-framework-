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
			bool complete = ProcessWorkflow(this.WorkflowInstance, completedSteps);

			Progress = completedSteps.Count(b => b) / completedSteps.Count;
			return complete ? ServiceOutcome.Success : ServiceOutcome.Unspecified;
		}

		bool ProcessWorkflow(WorkflowNodeInstance nodeInstance, List<bool> completedSteps)
		{
			bool complete;

			if (nodeInstance.Node is WorkflowNodeGroup)
			{
				var group = (WorkflowNodeGroup)nodeInstance.Node;
				complete = true;

				foreach (WorkflowNodeInstance child in nodeInstance.Children)
				{
					var stepComplete = ProcessWorkflow(child, completedSteps);
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
					nodeInstance.Instance.Start();
				}

				complete = nodeInstance.Instance.State == ServiceState.Ended;
				completedSteps.Add(complete);
			}
			else
				throw new NotSupportedException(String.Format("Workflow node type '{0}' not supported.", nodeInstance.Node.GetType()));

			return complete;
		}
	}
}
