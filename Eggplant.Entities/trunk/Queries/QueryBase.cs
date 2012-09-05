using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant2.Model;
using Eggplant2.Persistence;
using System.Data;

namespace Eggplant2.Queries
{
	public abstract class QueryBase
	{
		public EntitySpace EntitySpace { get; internal set; }
		public virtual PersistenceConnection Connection { get; set; }
		public IInboundMappingContext MappingContext { get; internal set; }
		public List<IEntityProperty> SelectList { get; private set; }
		public string FilterExpression { get; set; }
		public List<KeyValuePair<IEntityProperty, SortOrder>> SortingList { get; private set; }
		public Dictionary<string, QueryParameter> Parameters { get; private set; }
		public bool IsPrepared { get; private set; }

		public QueryBase()
		{
			this.IsPrepared = false;
			this.SelectList = new List<IEntityProperty>();
			this.SortingList = new List<KeyValuePair<IEntityProperty, SortOrder>>();
			this.Parameters = new Dictionary<string, QueryParameter>();
		}

		public QueryBase Select(params IEntityProperty[] properties)
		{
			this.SelectList.AddRange(properties);
			return this;
		}

		public QueryBase Filter(string filter)
		{
			this.FilterExpression = filter;
			return this;
		}

		public QueryBase Sort(IEntityProperty property, SortOrder order)
		{
			this.SortingList.Add(new KeyValuePair<IEntityProperty, SortOrder>(property, order));
			return this;
		}

		public QueryBase Param(string name, object value, DbType? dbType = null, int? size = null)
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

		protected void Prepare(SubqueryTemplate template)
		{
			// .....................................
			// Columns

			var columns = new StringBuilder();
			int columnCount = 0;
			foreach (var condition in template.Columns)
			{
				if (!condition.Value(this))
					continue;

				// Add the column name
				columns.Append(condition.Key);

				columnCount++;
				if (columnCount < template.Columns.Count)
					columns.Append(", ");
			}

			// .....................................
			// Filters

			var filters = new StringBuilder();
			for (int i = 0; i < this.FilterExpressions.Length; i++)
			{
				FilterExpression expression = this.FilterExpressions[i];

			}
		}
	}

	public class QueryParameter
	{
		public string Name;
		public object Value;
		public DbType? DbType;
		public int? Size;
	}
}
