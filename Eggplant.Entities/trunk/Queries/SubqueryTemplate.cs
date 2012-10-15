using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;

namespace Eggplant.Entities.Queries
{
	public class SubqueryTemplate
	{
		public QueryTemplate Template { get; set; }
		public int Index { get; set; }
		public string DataSet { get; set; }
		public string CommandText { get; set; }
		public Dictionary<string, Func<QueryBase, bool>> Columns { get; private set; }
		public Dictionary<string, SubqueryParameter> Parameters { get; private set; }

		public SubqueryTemplate()
		{
			this.Columns = new Dictionary<string, Func<QueryBase, bool>>();
			this.Parameters = new Dictionary<string, SubqueryParameter>();
		}

		public SubqueryTemplate Column(string columnName, IEntityProperty condition)
		{
			return Column(columnName, query => query.SelectList.Count == 0 || query.SelectList.Contains(condition));
		}

		public SubqueryTemplate Column(string columnName, Func<QueryBase, bool> condition)
		{
			this.Columns[columnName] = condition;

			return this;
		}

		public new SubqueryTemplate Param(string name, DbType dbType, int? size = null)
		{
			this.Parameters[name] = new SubqueryParameter()
			{
				Name = name,
				DbType = dbType,
				Size = size
			};

			return this;
		}

		public new SubqueryTemplate Param(string name, Func<Query, object> valueFunction, DbType? dbType = null, int? size = null)
		{
			this.Parameters[name] = new SubqueryParameter()
			{
				Name = name,
				ValueFunction = valueFunction,
				DbType = dbType,
				Size = size
			};

			return this;
		}

		public new SubqueryTemplate Param(string name, object value, DbType? dbType = null, int? size = null)
		{
			SubqueryParameter param;
			if (dbType != null || size != null || !this.Parameters.TryGetValue(name, out param))
			{
				this.Parameters[name] = param = new SubqueryParameter()
				{
					Name = name,
					DbType = dbType,
					Size = size
				};
			}
			param.Value = value;

			return this;
		}


		internal Subquery Start(PersistenceConnection connection)
		{
			Subquery subquery = new Subquery();
			foreach (SubqueryParameter parameter in this.Parameters.Values)
				subquery.Parameters.Add(parameter.Name, parameter);

			return subquery;
		}
	}

	public class SubqueryParameter
	{
		public string Name;
		public object Value;
		public Func<Query, object> ValueFunction;
		public DbType? DbType;
		public int? Size;
	}


}
