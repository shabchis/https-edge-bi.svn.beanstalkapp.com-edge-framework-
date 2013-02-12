using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Cache
{
	public class EntityCacheManager
	{
		Dictionary<IdentityDefinition, EntityCacheTable> _caches = new Dictionary<IdentityDefinition,EntityCacheTable>();

		public object Get(Identity id)
		{
			EntityCacheTable cache;
			if (!_caches.TryGetValue(id.IdentityDefinition, out cache))
				return null;
			return cache.Get(id);
		}
		
		public T Get<T>(Identity id)
		{
			return (T) Get(id);
		}

		public IEnumerable Get(IdentityDefinition identityDefinition)
		{
			EntityCacheTable cache;
			if (!_caches.TryGetValue(identityDefinition, out cache))
				yield break;
			foreach (object entity in cache.Get())
				yield return entity;
		}

		public IEnumerable<T> Get<T>(IdentityDefinition identityDefinition)
		{
			return Get(identityDefinition).OfType<T>();
		}

		public IEnumerable<T> Get<T>()
		{
			IEnumerable<T> union = null;
			foreach (var cacheType in _caches)
			{
				var entities = cacheType.Value.Get().OfType<T>();
				if (union == null)
					union = entities;
				else
					union = union.Union(entities);
			}

			return union;
		}

		public void Update(IdentityDefinition def, IDictionary<IEntityProperty, object> propertyValues)
		{
			EntityCacheTable cache;
			if (!_caches.TryGetValue(def, out cache))
				throw new EntityCacheException(String.Format("No cache is available for the identity definition '{0}'.", def));

			cache.Update(propertyValues);
		}

		public object Put(IdentityDefinition def, object entity, IEntityProperty[] activeProperites)
		{
			EntityCacheTable cache;
			if (!_caches.TryGetValue(def, out cache))
				_caches[def] = cache = new EntityCacheTable(def);

			return cache.Put(entity, activeProperites);
		}

		public T Put<T>(IdentityDefinition def, T entity, IEntityProperty[] activeProperites)
		{
			return (T)Put(def, (object)entity, activeProperites);
		}

		/*
		public void Put(object entity, IEntityProperty[] activeProperites)
		{
			if (entity == null)
				throw new ArgumentNullException("entity");

			foreach (var cacheType in _caches)
			{
				cacheType.Value.Put(entity, activeProperites);
			}
		}
		*/
	}

	[Serializable]
	public class EntityCacheException : Exception
	{
		public EntityCacheException() { }
		public EntityCacheException(string message) : base(message) { }
		public EntityCacheException(string message, Exception inner) : base(message, inner) { }
		protected EntityCacheException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
