using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using Eggplant.Entities.Model;
using Eggplant.Entities.Queries;

namespace Eggplant.Entities.Persistence
{
	public class CollectionMappingContext<T, V> : MappingContext<ICollection<V>>
	{
		public T CollectionParent;
		public EntityProperty<T, V> ValueProperty;

		internal CollectionMappingContext(QueryBase query, Mapping<ICollection<V>> mapping, MappingDirection dir)
			: base(query, mapping, dir)
		{
		}

		public MappingContext<V> Value
		{
			get { return (MappingContext<V>)this.SubMappings[ValueProperty]; }
		}
	}

}
