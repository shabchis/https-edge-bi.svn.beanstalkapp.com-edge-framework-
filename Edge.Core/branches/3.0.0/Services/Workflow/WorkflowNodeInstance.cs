using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services.Workflow
{
	public class WorkflowNodeInstance
	{
		public WorkflowNodeInstance Root;
		public WorkflowNodeInstance Parent;
		public WorkflowNode Node;
		public ServiceInstance Instance;
		public List<WorkflowNodeInstance> Children { get; private set; }

		private WorkflowNodeInstance()
		{
			this.Children = new List<WorkflowNodeInstance>();
		}

		public static WorkflowNodeInstance FromConfiguration(WorkflowServiceConfiguration configuration)
		{
			var nodeInstance = new WorkflowNodeInstance()
			{
				Node = configuration.Workflow,
			};
			nodeInstance.Root = nodeInstance;

			nodeInstance.ExpandInstances();
			return nodeInstance;
		}

		private void ExpandInstances()
		{
			var group = this.Node as WorkflowNodeGroup;
			if (group == null)
				return;
			
			if (group.Nodes == null)
				return;

			foreach (WorkflowNode node in group.Nodes)
			{
				var child = new WorkflowNodeInstance()
				{
					Node = node,
					Root = this.Root,
					Parent = this
				};

				child.ExpandInstances();
				this.Children.Add(child);
			}
		}
	}
}
