using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services.Workflow
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

	public enum GroupMode
	{
		Linear,
		Parallel
	}

	public class Group : WorkflowWorkNode
	{
		public GroupMode Mode {get; set; }
		public List<WorkflowNode> Nodes { get; set; }
	}

	public class Step : WorkflowWorkNode
	{
		public ServiceConfiguration ServiceConfiguration;
	}

	/*
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
	*/


	/*
	public class End: WorkflowNode
	{
		public End(ServiceOutcome outcome) { this.Outcome = outcome; }
		public ServiceOutcome Outcome;
	}
	*/
}
