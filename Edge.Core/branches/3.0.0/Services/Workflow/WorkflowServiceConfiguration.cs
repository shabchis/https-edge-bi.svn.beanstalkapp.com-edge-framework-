using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Edge.Core.Services.Workflow
{
	[Serializable]
	public class WorkflowServiceConfiguration: ServiceConfiguration
	{
		public const string Self = "SELF";
		WorkflowNodeGroup _workflow = new WorkflowNodeGroup { Name = Self, Mode = WorkflowNodeGroupMode.Linear };
		public WorkflowNodeGroup Workflow { get { return _workflow; } set { EnsureUnlocked(); _workflow = value; } }

		public WorkflowServiceConfiguration()
		{
			ServiceClass = typeof(WorkflowService).FullName;
		}

		protected override void Serialize(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("_workflow", _workflow);
		}

		protected WorkflowServiceConfiguration(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}

		protected override void Deserialize(SerializationInfo info, StreamingContext context)
		{
			_workflow = (WorkflowNodeGroup)info.GetValue("_workflow", typeof(WorkflowNodeGroup));
		}

		protected override IEnumerable<ILockable> GetLockables()
		{
			base.GetLockables();
			yield return Workflow;
		}

		protected override void CopyConfigurationData(ServiceConfiguration sourceConfig, ServiceConfiguration targetConfig)
		{
			base.CopyConfigurationData(sourceConfig, targetConfig);

			var sourceWorkflow = (WorkflowServiceConfiguration)sourceConfig;
			var targetWorkflow = (WorkflowServiceConfiguration)targetConfig;

			// copy workflow definitions mode and steps (clone steps according to source and derive configuration)
			targetWorkflow.Workflow.Mode = sourceWorkflow.Workflow.Mode;
			targetWorkflow.Workflow.Nodes = new LockableList<WorkflowNode>();

			foreach (var workflowStep in sourceWorkflow.Workflow.Nodes.Cast<WorkflowStep>().Where(workflowStep => workflowStep != null))
			{
				targetWorkflow.Workflow.Nodes.Add(new WorkflowStep
					{
						Name = workflowStep.Name,
						ServiceConfiguration = workflowStep.ServiceConfiguration.Derive(ServiceConfigurationLevel.Template, workflowStep.ServiceConfiguration)
					});
			}

			//targetWorkflow._workflow = sourceWorkflow._workflow;
		}

		public override ServiceProfile Profile
		{
			get { return base.Profile; }
			internal set
			{
				base.Profile = value;
				// set profile for all workflow childs
				if (Workflow.Nodes != null)
				{
					foreach (var step in Workflow.Nodes.Select(node => node as WorkflowStep).Where(step => step != null && step.ServiceConfiguration != null))
					{
						step.ServiceConfiguration.Profile = value;
					}
				}
			}
		}

		//public override ServiceConfigurationLevel ConfigurationLevel
		//{
		//	get { return base.ConfigurationLevel; }
		//	internal set
		//	{
		//		base.ConfigurationLevel = value;
		//		// set profile for all workflow childs
		//		if (Workflow.Nodes != null)
		//		{
		//			foreach (var step in Workflow.Nodes.Select(node => node as WorkflowStep).Where(step => step != null && step.ServiceConfiguration != null))
		//			{
		//				step.ServiceConfiguration.ConfigurationLevel = value;
		//			}
		//		}
		//	}
		//}
	}

	[Serializable]
	public abstract class WorkflowNode: Lockable
	{
		string _name;
		public string Name { get { return _name; } set { EnsureUnlocked(); _name = value; } }
	}

	[Serializable]
	public abstract class WorkflowWorkNode:WorkflowNode
	{
		WorkflowNodeFailureBehavior _failureBehavior = WorkflowNodeFailureBehavior.Terminate;
		public WorkflowNodeFailureBehavior FailureBehavior { get {return _failureBehavior; } set { EnsureUnlocked(); _failureBehavior = value; }}
	}

	public enum WorkflowNodeFailureBehavior
	{
		Continue,
		Terminate
	}

	public enum WorkflowNodeGroupMode
	{
		Linear,
		Parallel
	}

	[Serializable]
	public class WorkflowNodeGroup : WorkflowWorkNode
	{
		WorkflowNodeGroupMode _mode;
		LockableList<WorkflowNode> _nodes;

		public WorkflowNodeGroupMode Mode { get {return _mode; } set { EnsureUnlocked(); _mode = value; }}
		public LockableList<WorkflowNode> Nodes { get {return _nodes; } set { EnsureUnlocked(); _nodes = value; }}

		#region Lockable Members
		//=================

		protected override IEnumerable<ILockable> GetLockables()
		{
			base.GetLockables();
			yield return Nodes;
		}

		//=================
		#endregion
	}

	[Serializable]
	public class WorkflowStep : WorkflowWorkNode
	{
		ServiceConfiguration _serviceConfiguration;
		public ServiceConfiguration ServiceConfiguration { get {return _serviceConfiguration; } set { EnsureUnlocked(); _serviceConfiguration = value; }}

		#region Lockable Members
		//=================

		protected override IEnumerable<ILockable> GetLockables()
		{
			base.GetLockables();
			yield return ServiceConfiguration;
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
