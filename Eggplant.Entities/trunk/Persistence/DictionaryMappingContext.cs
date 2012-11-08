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
	public class DictionaryMappingContext<T, K, V> : MappingContext<IDictionary<K, V>>
	{
		public T CollectionParent;
		public EntityProperty<T, K> KeyProperty;
		public EntityProperty<T, V> ValueProperty;

		internal DictionaryMappingContext(QueryBase query, Mapping<IDictionary<K, V>> mapping, MappingDirection dir)
			: base(query, mapping, dir)
		{
		}

		public MappingContext<K> Key
		{
			get { return (MappingContext<K>)this.SubMappings[KeyProperty]; }
		}

		public MappingContext<V> Value
		{
			get { return (MappingContext<V>)this.SubMappings[ValueProperty]; }
		}

	}
}
