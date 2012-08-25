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

			bool complete = ProcessWorkflow(this.WorkflowInstance);
			return complete ? ServiceOutcome.Success : ServiceOutcome.Unspecified;
		}

		bool ProcessWorkflow(WorkflowNodeInstance nodeInstance)
		{
			bool complete;

			if (nodeInstance.Node is Group)
			{
				var group = (Group)nodeInstance.Node;
				complete = true;

				foreach (WorkflowNodeInstance child in nodeInstance.Children)
				{
					var stepComplete = ProcessWorkflow(child);
					complete &= stepComplete;
					if (!stepComplete && group.Mode == GroupMode.Linear)
						break;
				}
			}
			else if (nodeInstance.Node is Step)
			{
				var step = (Step)nodeInstance.Node;
				if (nodeInstance.Instance == null)
				{
					throw new NotImplementedException();
					//nodeInstance.Instance = Environment.NewServiceInstance(step.ServiceConfiguration, this);
					//nodeInstance.Instance.Start();
				}

				complete = nodeInstance.Instance.State == ServiceState.Ended;
			}
			else
				throw new NotSupportedException(String.Format("Workflow node type '{0}' not supported.", nodeInstance.Node.GetType()));

			return complete;
		}
	}
}
