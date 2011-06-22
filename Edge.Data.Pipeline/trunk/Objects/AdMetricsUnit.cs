using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Pipeline;

namespace Edge.Data.Objects
{
	public class AdMetricsUnit
	{
		public readonly Guid Guid = Guid.NewGuid();

		public DateTime PeriodStart;
		public DateTime PeriodEnd;
		
		public Ad Ad; //table
		public List<Target> TargetMatches; //table

		public Currency Currency;

		public Dictionary<Measure, double> MeasureValues=new Dictionary<Measure,double>();

	}
}
