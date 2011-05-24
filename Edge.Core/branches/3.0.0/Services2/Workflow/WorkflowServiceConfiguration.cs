using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services2.Workflow
{
	public class WorkflowServiceConfiguration: ServiceConfiguration
	{
		public const string Self = "SELF";
		public Group Workflow = new Group() { Name = Self, Mode = GroupMode.Linear };
	}

	public abstract class WorkflowNode
	{
		public string Name;
	}

	public abstract class WorkflowWorkNode:WorkflowNode
	{
		public WorkflowNodeFailureBehavior FailureBehavior = WorkflowNodeFailureBehavior.Terminate;
	}

	public enum WorkflowNodeFailureBehavior
	{
		Continue,
		Terminate
	}

	public class Group : WorkflowWorkNode
	{
		public GroupMode Mode = GroupMode.Linear;
		public List<WorkflowNode> Nodes;
	}

	public class Step : WorkflowWorkNode
	{
		public ServiceConfiguration Service;
	}

	public class Condition:WorkflowNode
	{
		public Func<WorkflowNodeInstance, bool> Test;
		public WorkflowNode MoveTo;

		public Condition(Func<WorkflowNodeInstance, bool> test)
		{
			Test = test;
		}

	}

	public class If : Condition
	{
		public If(Func<WorkflowNodeInstance, bool> test)
			: base(test)
		{
		}

		public WorkflowNode Then;
		public List<Condition> ElseIf;
		public Condition Else;
	}


	public enum GroupMode
	{
		Linear,
		Parallel
	}

	public class End: WorkflowNode
	{
		public End(ServiceOutcome outcome) { this.Outcome = outcome; }
		public ServiceOutcome Outcome;
	}

	public class WorkflowNodeInstance
	{
		Dictionary<string, WorkflowNodeInstance> _children;

		public WorkflowNodeInstance Root;
		public WorkflowNodeInstance Parent;
		public WorkflowNode Node;
		public ServiceInstance Instance;

		public ICollection<WorkflowNodeInstance> Children
		{
			get { return _children.Values; }
		}

		public WorkflowNodeInstance this[string name]
		{
			get { return _children[name]; }
		}
		public WorkflowNodeInstance this[int name]
		{
			get { return _children[name.ToString()]; }
		}
	}
}
