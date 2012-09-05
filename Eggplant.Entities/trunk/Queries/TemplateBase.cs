using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;

namespace Eggplant.Entities.Queries
{
	public abstract class TemplateBase
	{
		public Dictionary<string, QueryParameter> Parameters { get; private set; }

		public TemplateBase()
		{
			this.Parameters = new Dictionary<string, QueryParameter>();
		}

		public TemplateBase Param(string name, DbType dbType, int? size = null)
		{
			this.Parameters[name] = new QueryParameter()
			{
				Name = name,
				DbType = dbType,
				Size = size
			};

			return this;
		}

		public TemplateBase Param(string name, object value, DbType? dbType = null, int? size = null)
		{
			QueryParameter param;
			if (dbType != null || size != null || !this.Parameters.TryGetValue(name, out param))
			{
				this.Parameters[name] = param = new QueryParameter()
				{
					Name = name,
					DbType = dbType,
					Size = size
				};
			}
			param.Value = value;

			return this;
		}
	}

}
