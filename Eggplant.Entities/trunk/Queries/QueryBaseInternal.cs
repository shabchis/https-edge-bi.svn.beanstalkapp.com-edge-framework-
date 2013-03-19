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
		public Dictionary<string, QueryParameter> Parameters { get; private set; }
		internal Dictionary<string, PersistenceParameter> PersistenceParameters { get; private set; }

		public QueryBaseInternal()
		{
			this.PersistenceParameters = new Dictionary<string, PersistenceParameter>();
			this.Parameters = new Dictionary<string, QueryParameter>();
		}

		internal void PersistenceParam(string name, object value, PersistenceParameterOptions options)
		{
			PersistenceParameter param;
			if (options != null || !this.PersistenceParameters.TryGetValue(name, out param))
			{
				this.PersistenceParameters[name] = param = new PersistenceParameter(name, value, options);
			}
			param.Value = value;
		}

		public V Param<V>(string paramName)
		{
			QueryParameter param;
			if (!this.Parameters.TryGetValue(paramName, out param))
				throw new ArgumentException(String.Format("Parameter '{0}' is not defined.", paramName), "paramName");

			return (V) param.Value;
		}
	}
}
