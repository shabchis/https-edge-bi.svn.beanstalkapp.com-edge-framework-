using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Eggplant2.Model;
using Eggplant2.Persistence;

namespace Eggplant2.Queries
{
	public class SubqueryTemplate : TemplateBase
	{
		public QueryTemplate Template { get; set; }
		public int Index { get; set; }
		public string ResultSet { get; set; }
		public string CommandText { get; set; }
		public Dictionary<string, Func<QueryBase, bool>> Columns { get; private set; }

		public SubqueryTemplate()
		{
			this.Columns = new Dictionary<string, Func<QueryBase, bool>>();
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
			return (SubqueryTemplate) base.Param(name, dbType, size);
		}

		public new SubqueryTemplate Param(string name, object value, DbType? dbType = null, int? size = null)
		{
			return (SubqueryTemplate)base.Param(name, value, dbType, size);
		}


		internal Subquery Start(PersistenceConnection connection)
		{
			Subquery subquery = new Subquery();
			foreach (QueryParameter parameter in this.Parameters.Values)
				subquery.Parameters.Add(parameter.Name, parameter);

			return subquery;
		}
	}


}
