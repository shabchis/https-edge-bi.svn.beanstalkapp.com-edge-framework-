using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Cache
{
	internal class EntityCacheEntry<T>
	{
		public T Object;
		public IEntityProperty[] ActiveProperties;
		public DateTime TimeCreated;
		public DateTime TimeUpdated;
	}
}
