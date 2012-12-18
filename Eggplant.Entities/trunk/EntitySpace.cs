using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Persistence;
using Eggplant.Entities.Model;
using Eggplant.Entities.Queries;
using System.Reflection;

namespace Eggplant.Entities
{
	public class EntitySpace
	{
		public bool AutoRegisterDefinitions = true;
		public string AutoRegisterStaticDefinitionFieldName = "Definition";

		public Dictionary<Type, IEntityDefinition> Definitions { get; private set; }
		
		public EntitySpace()
		{
			this.Definitions = new Dictionary<Type,IEntityDefinition>();
		}

		public EntityDefinition<T> GetDefinition<T>()
		{
			Type entityType = typeof(T);
			IEntityDefinition def;
			if (!this.Definitions.TryGetValue(entityType, out def))
			{
				if (!this.AutoRegisterDefinitions)
					return null;

				FieldInfo defField = entityType.GetField(this.AutoRegisterStaticDefinitionFieldName, BindingFlags.Public | BindingFlags.Static);
				if (defField == null || !typeof(EntityDefinition<T>).IsAssignableFrom(defField.FieldType))
					return null;
					/*
					throw new ArgumentException(String.Format(
						"The type '{0}' does not have a static field '{1}' of type 'EntityDefinition<{0}>'. Use RegisterDefinition to manually register a EntityDefinition object for this type.",
						entityType.Name,
						this.AutoRegisterStaticDefinitionFieldName)
					);
					*/

				def = (IEntityDefinition) defField.GetValue(null);
			}

			return (EntityDefinition<T>)def;
		}

		public QueryTemplate<T> CreateQueryTemplate<T>(Mapping<T> inboundMapping = null)
		{
			return new QueryTemplate<T>(this)
			{
				InboundMapping = inboundMapping
			};
		}

		public Mapping<T> CreateMapping<T>(Action<Mapping<T>> initFunction = null)
		{
			SubqueryMapping<T> mapping = new SubqueryMapping<T>(null, this);
			if (initFunction != null)
				initFunction(mapping);
			return mapping;
		}
	}
}
