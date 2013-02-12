using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using System.Collections;

namespace Eggplant.Entities.Cache
{
	internal class EntityCacheTable
	{
		public readonly IdentityDefinition IdentityDefinition;
		private Dictionary<Identity, EntityCacheEntry> _entities = new Dictionary<Identity,EntityCacheEntry>();

		public EntityCacheTable(IdentityDefinition identityDefinition)
		{
			this.IdentityDefinition = identityDefinition;
		}

		public object Get(Identity id)
		{
			EntityCacheEntry entry;
			if (!_entities.TryGetValue(id, out entry))
				return null;

			return entry.Entity;
		}

		public IEnumerable Get()
		{
			return _entities.Values.Select<EntityCacheEntry, object>(entry => entry.Entity);
		}

		public object Put(object entity, IEntityProperty[] activeProperites)
		{
			Identity id = this.IdentityDefinition.IdentityOf(entity);

			EntityCacheEntry entry;
			if (!_entities.TryGetValue(id, out entry))
			{
				// no entry yet, add it
				entry = new EntityCacheEntry(entity, activeProperites);
				_entities.Add(id, entry);
			}
			else
			{
				entry.Update(entity, activeProperites);
			}

			return entry.Entity;
		}

		public void Update(IDictionary<IEntityProperty, object> propertyValues)
		{
			Identity id = this.IdentityDefinition.IdentityFromValues(propertyValues);

			EntityCacheEntry entry;
			if (!_entities.TryGetValue(id, out entry))
				throw new EntityCacheException(String.Format("There is no entry available for the identity '{0}'.", id));

			entry.Update(propertyValues);
		}
	}
}
