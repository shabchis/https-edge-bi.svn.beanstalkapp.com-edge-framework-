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
				throw new NotImplementedException("Filtering not yet implemented in this version.");
				/*
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
				*/
			}
		}


		internal QueryBase Sort(IEntityProperty property, SortOrder order)
		{
			this.SortingList.Add(new SortingDefinition() { Property = property, SortOrder = order });
			return this;
		}

		public V Input<V>(string inputName)
		{
			return (V) GetQueryInput<V>(inputName).Value;
		}

		public void Input<V>(string inputName, V value)
		{
			GetQueryInput<V>(inputName).Value = value;
		}

		private QueryInput GetQueryInput<V>(string inputName)
		{
			QueryInput input;
			if (!this.Inputs.TryGetValue(inputName, out input))
				throw new ArgumentException(String.Format("Input '{0}' is not defined in the query template.", inputName), "inputName");
			if (!input.InputType.IsAssignableFrom(typeof(V)))
				throw new ArgumentException(String.Format("Input '{0}' requires values of type {1}.", inputName, input.InputType), "value");
			return input;
		}
	}


	public class SortingDefinition
	{
		public IEntityProperty Property;
		public SortOrder SortOrder;
	}

}
