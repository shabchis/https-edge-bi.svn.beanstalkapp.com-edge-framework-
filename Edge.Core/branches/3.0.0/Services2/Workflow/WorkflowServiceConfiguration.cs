using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Core.Services2
{
	/*
	public class WorkflowServiceConfiguration: ServiceConfiguration
	{
		public List<WorkflowNode> Nodes;
	}
	*/

	public abstract class WorkflowNode
	{
	}

	public class ConditionNode:WorkflowNode
	{
	}
}
