using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Eggplant.Entities.Persistence;

namespace Eggplant.Entities.Queries
{
	public abstract class QueryBaseInternal
	{
		public Dictionary<string, QueryInput> Inputs { get; private set; }

		public QueryBaseInternal()
		{
			this.Inputs = new Dictionary<string, QueryInput>();
		}

		public V Input<V>(string inputName)
		{
			QueryInput input;
			if (!this.Inputs.TryGetValue(inputName, out input))
				throw new ArgumentException(String.Format("Parameter '{0}' is not defined.", inputName), "paramName");

			return (V) input.Value;
		}
	}
}
