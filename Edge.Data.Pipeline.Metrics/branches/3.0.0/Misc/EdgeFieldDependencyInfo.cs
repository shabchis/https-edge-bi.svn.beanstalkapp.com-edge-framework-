using System.Collections.Generic;
using Edge.Data.Objects;
namespace Edge.Data.Pipeline.Metrics.Misc
{
	public class MetricsDependencyInfo
	{
		public List<EdgeFieldDependencyInfo> Dependencies { get; set; }
	}

	public class EdgeFieldDependencyInfo
	{
		public EdgeField Field { get; set; }
		public int Level { get; set; }
		public List<EdgeField> DependentFields { get; set; }

		public EdgeFieldDependencyInfo()
		{
			DependentFields = new List<EdgeField>();
		}
	}
}
