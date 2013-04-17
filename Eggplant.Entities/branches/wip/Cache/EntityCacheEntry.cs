using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Eggplant.Entities.Cache
{
	internal class EntityCacheEntry
	{
		private object _entity;
		Dictionary<IEntityProperty, object> _properties = new Dictionary<IEntityProperty, object>();

		public DateTime TimeCreated { get; private set; }
		public DateTime TimeUpdated { get; private set; }

		public EntityCacheEntry(object entity, IEntityProperty[] activeProperties)
		{
			this.TimeCreated = DateTime.Now;
			this.Update(entity, activeProperties);
		}

		public object Entity
		{
			get { return _entity; }
		}

		public void Update(IDictionary<IEntityProperty, object> propertyValues)
		{
			foreach (var propVal in propertyValues)
			{
				propVal.Key.SetValue(_entity, propVal.Value);
				object nothing;
				if (!_properties.TryGetValue(propVal.Key, out nothing))
					_properties.Add(propVal.Key, null);
			}

			TimeUpdated = DateTime.Now;
		}

		public void Update(object entity, IEntityProperty[] activeProperties)
		{
			if (_entity == null)
			{
				_entity = entity;
				for (int i = 0; i < activeProperties.Length; i++)
					_properties.Add(activeProperties[i], null);
			}
			else
			{
				for (int i = 0; i < activeProperties.Length; i++)
				{
					object nothing;
					IEntityProperty property = activeProperties[i];

					// Update the property value
					property.SetValue(_entity, property.GetValue(entity));

					if (!_properties.TryGetValue(property, out nothing))
						_properties.Add(property, null);
				}
			}

			TimeUpdated = DateTime.Now;
		}

		//public bool 
	}
}
