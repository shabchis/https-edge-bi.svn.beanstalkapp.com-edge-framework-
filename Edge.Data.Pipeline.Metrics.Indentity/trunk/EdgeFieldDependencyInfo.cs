using System.Collections.Generic;
using Edge.Data.Objects;

namespace Edge.Data.Pipeline.Metrics.Indentity
{
	public class EdgeFieldDependencyInfo
	{
		public EdgeField Field { get; set; }
		public int Depth { get; set; }
		public Dictionary<EdgeField, EdgeTypeField> DependentFields { get; set; }

		public EdgeFieldDependencyInfo()
		{
			DependentFields = new Dictionary<EdgeField, EdgeTypeField>();
			Depth = -1;
		}
	}
}
