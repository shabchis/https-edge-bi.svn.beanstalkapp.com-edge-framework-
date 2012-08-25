using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Edge.Core.Services.Workflow
{
	[Serializable]
	public class WorkflowServiceConfiguration: ServiceConfiguration
	{
		public const string Self = "SELF";
		Group _workflow = new Group() { Name = Self, Mode = GroupMode.Linear };
		public Group Workflow { get { return _workflow; } set { InnerLock.Ensure(); _workflow = value; } }

		public WorkflowServiceConfiguration()
		{
			this.ServiceClass = typeof(WorkflowService).FullName;
		}

		protected override void Serialize(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
		{
			info.AddValue("_workflow", _workflow);
		}

		protected override void Deserialize(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
		{
			_workflow = (Group)info.GetValue("_workflow", typeof(Group));
		}

		protected override IEnumerable<ILockable> GetLockables()
		{
			base.GetLockables();
			yield return this.Workflow;
		}
	}

	[Serializable]
	public abstract class WorkflowNode: Lockable
	{
		string _name;

		[DebuggerNonUserCode]
		public string Name { get { return _name; } set { InnerLock.Ensure(); _name = value; } }
	}

	[Serializable]
	public abstract class WorkflowWorkNode:WorkflowNode
	{
		WorkflowNodeFailureBehavior _failureBehavior = WorkflowNodeFailureBehavior.Terminate;

		[DebuggerNonUserCode]
		public WorkflowNodeFailureBehavior FailureBehavior { get {return _failureBehavior; } set { InnerLock.Ensure(); _failureBehavior = value; }}
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

	[Serializable]
	public class Group : WorkflowWorkNode
	{
		GroupMode _mode;
		LockableList<WorkflowNode> _nodes;

		[DebuggerNonUserCode]
		public GroupMode Mode { get {return _mode; } set { InnerLock.Ensure(); _mode = value; }}
		[DebuggerNonUserCode]
		public LockableList<WorkflowNode> Nodes { get {return _nodes; } set { InnerLock.Ensure(); _nodes = value; }}

		#region Lockable Members
		//=================

		protected override IEnumerable<ILockable> GetLockables()
		{
			yield return (ILockable)this.Nodes;
		}

		//=================
		#endregion
	}

	[Serializable]
	public class Step : WorkflowWorkNode
	{
		ServiceConfiguration _serviceConfiguration;

		[DebuggerNonUserCode]
		public ServiceConfiguration ServiceConfiguration { get {return _serviceConfiguration; } set { InnerLock.Ensure(); _serviceConfiguration = value; }}

		#region Lockable Members
		//=================

		protected override IEnumerable<ILockable> GetLockables()
		{
			yield return (ILockable)this.ServiceConfiguration;
		}

		//=================
		#endregion
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
