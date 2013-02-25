using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace Eggplant.Entities.Queries
{
	public abstract class QueryBaseInternal
	{
		public Dictionary<string, QueryParameter> Parameters { get; private set; }
		internal Dictionary<string, DbParameter> DbParameters { get; private set; }

		public QueryBaseInternal()
		{
			this.DbParameters = new Dictionary<string, DbParameter>();
			this.Parameters = new Dictionary<string, QueryParameter>();
		}

		internal void DbParam(string name, object value, DbType? dbType = null, int? size = null)
		{
			DbParameter param;
			if (dbType != null || size != null || !this.DbParameters.TryGetValue(name, out param))
			{
				this.DbParameters[name] = param = new DbParameter()
				{
					Name = name,
					DbType = dbType,
					Size = size
				};
			}
			param.Value = value;
		}

		internal void DbParam(string name, Func<Query, object> valueFunc, DbType? dbType = null, int? size = null)
		{
			this.DbParameters[name] = new DbParameter()
			{
				Name = name,
				ValueFunction = valueFunc,
				DbType = dbType,
				Size = size
			};
		}

		public V Param<V>(string paramName)
		{
			QueryParameter param;
			if (!this.Parameters.TryGetValue(paramName, out param))
				throw new ArgumentException(String.Format("Parameter '{0}' is not defined.", paramName), "paramName");

			return (V) param.Value;
		}
	}

	public class QueryParameter
	{
		public string Name;
		public Type ParameterType;
		public bool IsRequired;
		public object DefaultValue;
		public object EmptyValue;
		public object Value;

		public QueryParameter Clone()
		{
			return new QueryParameter()
			{
				Name = this.Name,
				ParameterType = this.ParameterType,
				IsRequired = this.IsRequired,
				DefaultValue = this.DefaultValue,
				EmptyValue = this.EmptyValue,
				Value = this.Value
			};
		}
	}

	public class DbParameter
	{
		public string Name;
		public object Value;
		public Func<Query, object> ValueFunction;
		public DbType? DbType;
		public int? Size;

		public DbParameter Clone()
		{
			return new DbParameter()
			{
				Name = this.Name,
				Value = this.Value,
				ValueFunction = this.ValueFunction,
				DbType = this.DbType,
				Size = this.Size
			};
		}
	}
}
