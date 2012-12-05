using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;
using System.Data;
using System.Collections;

namespace Eggplant.Entities.Queries
{
	public abstract class QueryBase : QueryBaseInternal
	{
		object[] _filter; 
		
		public EntitySpace EntitySpace { get; internal set; }
		public abstract PersistenceConnection Connection { get; internal set; }
		public IMapping Mapping { get; protected set; }
		public MappingContext MappingContext { get; protected set; }
		
		public virtual List<IEntityProperty> SelectList { get; private set; }
		public virtual List<SortingDefinition> SortingList { get; private set; }
		public bool IsPrepared { get; protected set; }
		

		public QueryBase()
		{
			this.IsPrepared = false;
			this.SelectList = new List<IEntityProperty>();
			this.SortingList = new List<SortingDefinition>();
		}


		internal QueryBase Select(params IEntityProperty[] properties)
		{
			this.SelectList.AddRange(properties);
			return this;
		}

		internal QueryBase Filter(params object[] filterExpression)
		{
			this.FilterExpression = filterExpression;
			return this;
		}

		public virtual object[] FilterExpression
		{
			get { return _filter; }
			set
			{
				if (value != null && value.Length > 0)
				{
					object[] copied = new object[value.Length];
					for (int i = 0; i < value.Length; i++)
					{
						object filter = value[i];
						if (!(filter is IEntityProperty) && !(filter is string))
							filter = new DbParameter() { Name = "@filterParam" + i.ToString(), Value = filter };
					}
					value = copied;
				}

				_filter = value;
			}
		}


		internal QueryBase Sort(IEntityProperty property, SortOrder order)
		{
			this.SortingList.Add(new SortingDefinition() { Property = property, SortOrder = order });
			return this;
		}

		public void Param<V>(string paramName, V value)
		{
			QueryParameter param;
			if (!this.Parameters.TryGetValue(paramName, out param))
				throw new ArgumentException(String.Format("Parameter '{0}' is not defined in the query template.", paramName), "paramName");
			if (!param.ParameterType.IsAssignableFrom(typeof(V)))
				throw new ArgumentException(String.Format("Parameter '{0}' requires values of type {1}.", paramName, param.ParameterType), "value");

			param.Value = value;
		}
	}


	public class SortingDefinition
	{
		public IEntityProperty Property;
		public SortOrder SortOrder;
	}

}
