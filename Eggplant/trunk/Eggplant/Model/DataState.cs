using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Model
{
	public enum DataState
	{
		Unchanged,
		Modified,
		Added,
		Deleted,
		Detached
	}
}
