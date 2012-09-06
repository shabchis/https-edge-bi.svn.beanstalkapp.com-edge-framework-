using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using System.Data.Common;

namespace Eggplant.Entities.Persistence
{
	public interface IInboundMapping
	{
		EntitySpace EntitySpace { get; }
		string DataSet { get; }
		Dictionary<IEntityProperty, IInboundMapping> SubMappings { get; }
	}

	public class InboundMapping<T>: IInboundMapping
	{
		internal InboundMapping(EntitySpace space)
		{
			this.EntitySpace = space;
			this.EntityDefinition = space.GetDefinition<T>();
			this.SubMappings = new Dictionary<IEntityProperty, IInboundMapping>();
		}

		// ===================================
		// Properties

		public EntitySpace EntitySpace { get; private set; }
		public EntityDefinition<T> EntityDefinition { get; private set; }
		public Delegate MappingFunction { get; set; }
		public Dictionary<IEntityProperty, IInboundMapping> SubMappings { get; private set; }
		public string DataSet { get; set; }

		// ===================================
		// Internal

		// ===================================
		// Inline definition helpers

		/// <summary>
		/// Inherits all mappings from the base base mapping.
		/// </summary>
		public InboundMapping<T> Inherit(IInboundMapping baseMapping, bool @override = false)
		{
			foreach (var mapping in baseMapping.SubMappings)
			{
				if (!@override && this.SubMappings.ContainsKey(mapping.Key))
					continue;

				this.SubMappings[mapping.Key] = mapping.Value;
			}

			return this;
		}

		/// <summary>
		/// Retrieves a field value when a scalar property needs mapping.
		/// </summary>
		public InboundMapping<T> Scalar<V>(IEntityProperty<V> property, string field)
		{
			return this.Scalar<V>(property, context => context.Assign(context.GetField<V>(field)));
		}

		/// <summary>
		/// Activates a function when a scalar property needs mapping.
		/// </summary>
		public InboundMapping<T> Scalar<V>(IEntityProperty<V> property, Action<InboundMappingContext<V>> function)
		{
			this.SubMappings[property] = new InboundMapping<V>(this.EntitySpace) { MappingFunction = function };
			return this;
		}

		public InboundMapping<T> Collection<V>(ICollectionProperty<V> collection, string dataSet, string field)
		{
			return this.Collection<V>(collection, dataSet, context => context.Assign(context.GetField<V>(field)));
		}

		public InboundMapping<T> Collection<V>(ICollectionProperty<V> collection, string dataSet, Action<InboundCollectionMappingContext<T,V>> function)
		{
			var collectionMapping = new InboundMapping<ICollection<V>>(this.EntitySpace) { DataSet = dataSet };

			collectionMapping.SubMappings[collection.Value] = new InboundMapping<V>(this.EntitySpace)
			{
				MappingFunction = function
			};

			this.SubMappings[collection] = collectionMapping;

			return this;
		}

		public InboundMapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string dataSet, string key, string value)
		{
			return this.Dictionary<K, V>(dictionary, dataSet,
				context => context.Assign(context.GetField<K>(key)),
				context => context.Assign(context.GetField<V>(value))
			);
		}

		public InboundMapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string dataSet, Action<InboundCollectionMappingContext<T,K>> key, string value)
		{
			return this.Dictionary<K, V>(dictionary, dataSet,
				key,
				context => context.Assign(context.GetField<V>(value))
			);
		}

		public InboundMapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string dataSet, string key, Action<InboundCollectionMappingContext<T,V>> value)
		{
			return this.Dictionary<K, V>(dictionary, dataSet,
				context => context.Assign(context.GetField<K>(key)),
				value
			);
		}

		public InboundMapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string dataSet, Action<InboundCollectionMappingContext<T,K>> key, Action<InboundCollectionMappingContext<T,V>> value)
		{
			var dictMapping = new InboundMapping<IDictionary<K,V>>(this.EntitySpace) { DataSet = dataSet, };

			dictMapping.SubMappings[dictionary.Key] = new InboundMapping<K>(this.EntitySpace)
			{
				MappingFunction = key
			};

			dictMapping.SubMappings[dictionary.Value] = new InboundMapping<V>(this.EntitySpace)
			{
				MappingFunction = value
			};

			this.SubMappings[dictionary] = dictMapping;

			return this;
		}
	}

	public interface IInboundMappingContext : IInboundMapping
	{
		PersistenceConnection Connection { get; }

		V GetField<V>(string field, Func<object,V> convertFunction = null);
		IInboundMappingContext Assign(object value);
	}

	public class InboundMappingContext<T> : InboundMapping<T>, IInboundMappingContext
	{
		public PersistenceConnection Connection { get; private set; }

		internal InboundMappingContext(InboundMapping<T> mapping, PersistenceConnection connection)
			: base(mapping.EntitySpace)
		{
			this.Connection = connection;
			this.DataSet = mapping.DataSet;
			this.MappingFunction = mapping.MappingFunction;
			foreach (var sub in mapping.SubMappings)
			{
				this.SubMappings.Add(sub.Key, sub.Key.CreateInboundContext(sub.Value, connection));
			}
		}

		public V GetField<V>(string field, Func<object, V> convertFunction = null)
		{
			throw new NotImplementedException();
		}

		public InboundMappingContext<T> Assign(object rawValue)
		{
			return this;
		}

		public InboundMappingContext<T> Assign(T value)
		{
			return this;
		}

		#region Explicit

		IInboundMappingContext IInboundMappingContext.Assign(object value)
		{
			return this.Assign(value);
		}

		#endregion
	}

	public class InboundCollectionMappingContext<T, V> : InboundMappingContext<ICollection<V>>
	{
		public T CollectionParent;
		public EntityProperty<T, V> ValueProperty;

		internal InboundCollectionMappingContext(InboundMapping<ICollection<V>> mapping, PersistenceConnection connection)
			: base(mapping, connection)
		{
		}

		public InboundMappingContext<V> Value
		{
			get { return (InboundMappingContext<V>)this.SubMappings[ValueProperty]; }
		}
	}

	public class InboundDictionaryMappingContext<T, K, V> : InboundMappingContext<IDictionary<K,V>>
	{
		public T CollectionParent;
		public EntityProperty<T, K> KeyProperty;
		public EntityProperty<T, V> ValueProperty;

		internal InboundDictionaryMappingContext(InboundMapping<IDictionary<K, V>> mapping, PersistenceConnection connection)
			: base(mapping, connection)
		{
		}

		public InboundMappingContext<K> Key
		{
			get { return (InboundMappingContext<K>)this.SubMappings[KeyProperty]; }
		}

		public InboundMappingContext<V> Value
		{
			get { return (InboundMappingContext<V>)this.SubMappings[ValueProperty]; }
		}

	}
}
