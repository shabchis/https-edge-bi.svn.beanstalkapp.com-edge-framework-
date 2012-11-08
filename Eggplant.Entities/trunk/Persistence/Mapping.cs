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
	public class Mapping<T>: IMapping
	{
		internal Mapping(EntitySpace space)
		{
			this.EntitySpace = space;
			this.EntityDefinition = space.GetDefinition<T>();
			this.SubMappings = new Dictionary<IEntityProperty, IMapping>();
		}

		// ===================================
		// Properties

		public EntitySpace EntitySpace { get; private set; }
		public EntityDefinition<T> EntityDefinition { get; private set; }
		public Delegate MappingFunction { get; set; }
		public IEntityProperty<T> Property { get; internal set; }
		public Dictionary<IEntityProperty, IMapping> SubMappings { get; private set; }
		public string ResultSetName { get; set; }
		public Func<IMappingContext, T> InstantiationFunction { get; set; }

		// ===================================
		// Internal

		// ===================================
		// Inline definition helpers

		/// <summary>
		/// Inherits all mappings from the base base mapping.
		/// </summary>
		public Mapping<T> Inherit(IMapping baseMapping, bool @override = false)
		{
			foreach (var mapping in baseMapping.SubMappings)
			{
				if (!@override && this.SubMappings.ContainsKey(mapping.Key))
					continue;

				this.SubMappings[mapping.Key] = mapping.Value;
			}

			return this;
		}

		public Mapping<T> Instantiate(Func<IMappingContext, T> instantiationFunction)
		{
			this.InstantiationFunction = instantiationFunction;

			return this;
		}

		/// <summary>
		/// Maps a result set field to a scalar property.
		/// </summary>
		public Mapping<T> Scalar<V>(IEntityProperty<V> property, string field)
		{
			return this.Scalar<V>(property, context =>
				{
					if (context.Direction == MappingDirection.Inbound)
						context.SetValue(context.GetField<V>(field));
					else
						context.SetField(field, context.Target);
				}
			);
		}

		/// <summary>
		/// Activates a function when a scalar property needs mapping.
		/// </summary>
		public Mapping<T> Scalar<V>(IEntityProperty<V> property, Action<MappingContext<V>> function)
		{
			this.SubMappings[property] = new Mapping<V>(this.EntitySpace) { Property = property, MappingFunction = function };
			return this;
		}

		public Mapping<T> Collection<V>(ICollectionProperty<V> collection, string resultSet, string field)
		{
			return this.Collection<V>(collection, resultSet, context =>
			{
				if (context.Direction == MappingDirection.Inbound)
					context.SetValue(context.GetField<V>(field));
				else
					context.SetField(field, context.Target);
			});
		}

		public Mapping<T> Collection<V>(ICollectionProperty<V> collection, string resultSet, Action<CollectionMappingContext<T, V>> function)
		{
			var collectionMapping = new Mapping<ICollection<V>>(this.EntitySpace) {ResultSetName = resultSet };

			collectionMapping.SubMappings[collection.Value] = new Mapping<V>(this.EntitySpace)
			{
				MappingFunction = function
			};

			this.SubMappings[collection] = collectionMapping;

			return this;
		}

		public Mapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string resultSet, string key, string value)
		{
			return this.Dictionary<K, V>(dictionary, resultSet,
				context =>
				{
					if (context.Direction == MappingDirection.Inbound)
						context.SetValue(context.GetField<K>(key));
					else
						context.SetField(key, context.Target);
				},
				context =>
				{
					if (context.Direction == MappingDirection.Inbound)
						context.SetValue(context.GetField<V>(value));
					else
						context.SetField(value, context.Target);
				}
			);
		}

		public Mapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string resultSet, Action<CollectionMappingContext<T, K>> key, string value)
		{
			return this.Dictionary<K, V>(dictionary, resultSet,
				key,
				context => context.SetValue(context.GetField<V>(value))
			);
		}

		public Mapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string resultSet, string key, Action<CollectionMappingContext<T, V>> value)
		{
			return this.Dictionary<K, V>(dictionary, resultSet,
				context => context.SetValue(context.GetField<K>(key)),
				value
			);
		}

		public Mapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string resultSet, Action<CollectionMappingContext<T, K>> key, Action<CollectionMappingContext<T, V>> value)
		{
			var dictMapping = new Mapping<IDictionary<K, V>>(this.EntitySpace) { ResultSetName = resultSet, };

			dictMapping.SubMappings[dictionary.Key] = new Mapping<K>(this.EntitySpace)
			{
				MappingFunction = key
			};

			dictMapping.SubMappings[dictionary.Value] = new Mapping<V>(this.EntitySpace)
			{
				MappingFunction = value
			};

			this.SubMappings[dictionary] = dictMapping;

			return this;
		}

		#region IMapping Members

		MethodInfo IMapping.InstantiationFunction
		{
			get { return this.InstantiationFunction.Method; }
		}

		IEntityProperty IMapping.Property
		{
			get { return this.Property; }
		}

		#endregion
	}
}
