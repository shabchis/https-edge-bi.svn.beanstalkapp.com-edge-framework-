using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Queries
{
	public enum QueryExecutionMode
	{
		Streaming,
		Buffered
	}

	public enum SortOrder
	{
		Ascending,
		Descending
	}
}
