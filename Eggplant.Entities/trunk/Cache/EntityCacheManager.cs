using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Cache
{
	public class EntityCacheManager
	{
		Dictionary<Type, IEntityCache> _caches;

		public T Get<T>(IdentityDefinition def, Identity id)
		{
			return (T) this._caches[typeof(T)].Get(def, id);
		}

		public T Get<T>(IdentityDefinition def, params object[] idParts)
		{
			return (T) this._caches[typeof(T)].Get(def, idParts);
		}

		public IEnumerable<T> Get<T>()
		{
			return (IEnumerable<T>)this._caches[typeof(T)].Get();
		}
	}
}
