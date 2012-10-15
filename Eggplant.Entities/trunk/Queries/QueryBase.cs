using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Persistence;
using System.Data;

namespace Eggplant.Entities.Queries
{
	public abstract class QueryBase
	{
		public EntitySpace EntitySpace { get; internal set; }
		public virtual PersistenceConnection Connection { get; set; }
		public IMappingContext MappingContext { get; internal set; }
		public List<IEntityProperty> SelectList { get; private set; }
		public string FilterExpression { get; set; }
		public List<KeyValuePair<IEntityProperty, SortOrder>> SortingList { get; private set; }
		public bool IsPrepared { get; private set; }

		public QueryBase()
		{
			this.IsPrepared = false;
			this.SelectList = new List<IEntityProperty>();
			this.SortingList = new List<KeyValuePair<IEntityProperty, SortOrder>>();
		}

		public QueryBase Select(params IEntityProperty[] properties)
		{
			this.SelectList.AddRange(properties);
			return this;
		}

		public QueryBase Filter(string filterExpression)
		{
			this.FilterExpression = filterExpression;
			return this;
		}

		public QueryBase Sort(IEntityProperty property, SortOrder order)
		{
			this.SortingList.Add(new KeyValuePair<IEntityProperty, SortOrder>(property, order));
			return this;
		}

		public QueryBase Column(string placeHolder, IEntityProperty property)
		{
			throw new NotImplementedException();
		}



		
	}

	
}
