﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using System.Collections;

namespace Eggplant.Entities.Cache
{
	internal class EntityCache<T> : IEntityCache
	{
		private Dictionary<IdentityDefinition, Dictionary<Identity, EntityCacheEntry<T>>> _objects;

		public EntityCache(params IdentityDefinition[] defs)
		{
			_objects = new Dictionary<IdentityDefinition, Dictionary<Identity, EntityCacheEntry<T>>>();
		}

		public T Update(T obj, IEntityProperty[] activeProperties)
		{
			throw new NotImplementedException();
		}

		public T Get(IdentityDefinition def, Identity id)
		{
			Dictionary<Identity, EntityCacheEntry<T>> dict;
			if (!_objects.TryGetValue(def, out dict))
			{
				dict = new Dictionary<Identity, EntityCacheEntry<T>>();
				_objects[def] = dict;
			}

			return dict[id].Object;
		}

		public T Get(IdentityDefinition def, params object[] idParts)
		{
			return Get(def, def.NewIdentity(idParts));
		}

		public IEnumerable<T> Get(IdentityDefinition def)
		{
			return this._objects[def].Values.Select<EntityCacheEntry<T>, T>(entry => entry.Object);
		}

		public void Add(IEnumerable<T> objects, IEntityProperty[] activeProperties)
		{
			lock (_objects)
			{
				foreach (T obj in objects)
				{
					foreach (var dictionaryDef in _objects)
					{
						Identity id = dictionaryDef.Key.IdentityOf(obj);
						EntityCacheEntry<T> entry;
						if (dictionaryDef.Value.TryGetValue(id, out entry))
						{
							// Update existing cache value
							//if (
						}
						else
						{
							entry = new EntityCacheEntry<T>()
							{
								Object = obj,
								ActiveProperties = activeProperties
							};
							dictionaryDef.Value.Add(id, entry);
						}
					}
				}
			}
		}

		#region IEntityCache Members

		object IEntityCache.Get(IdentityDefinition def, Identity id)
		{
			return this.Get(def, id);
		}

		object IEntityCache.Get(IdentityDefinition def, params object[] idParts)
		{
			return this.Get(def, idParts);
		}

		System.Collections.IEnumerable IEntityCache.Get()
		{
			throw new NotImplementedException();
		}

		void IEntityCache.Add(IEnumerable objects, IEntityProperty[] activeProperties)
		{
			this.Add((IEnumerable<T>)objects, activeProperties);
		}

		#endregion
	}
}
