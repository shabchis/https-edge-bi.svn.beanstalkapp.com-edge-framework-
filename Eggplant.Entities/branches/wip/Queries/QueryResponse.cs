using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Queries
{
	public class QueryResponse
	{
	}

	public class QueryResponse<T>
	{
		public Dictionary<string, QueryOutput> Outputs { get; private set; }
		public IEnumerable<T> Results { get; private set; }
	}
}
