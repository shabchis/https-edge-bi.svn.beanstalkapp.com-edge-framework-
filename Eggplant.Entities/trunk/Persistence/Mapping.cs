using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;
using System.Data.Common;

namespace Eggplant.Entities.Persistence
{
	public enum MappingDirection
	{
		Inbound,
		Outbound
	}

	public interface IMapping
	{
		EntitySpace EntitySpace { get; }
		string DataSet { get; }
		Dictionary<IEntityProperty, IMapping> SubMappings { get; }
		MappingDirection Direction { get; }
	}

	public class Mapping<T>: IMapping
	{
		internal Mapping(EntitySpace space, MappingDirection direction)
		{
			this.EntitySpace = space;
			this.EntityDefinition = space.GetDefinition<T>();
			this.Direction = direction;
			this.SubMappings = new Dictionary<IEntityProperty, IMapping>();
		}

		// ===================================
		// Properties

		public EntitySpace EntitySpace { get; private set; }
		public EntityDefinition<T> EntityDefinition { get; private set; }
		public Delegate MappingFunction { get; set; }
		public Dictionary<IEntityProperty, IMapping> SubMappings { get; private set; }
		public string DataSet { get; set; }
		public MappingDirection Direction { get; private set; }

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

		/// <summary>
		/// Maps a dataset field to a scalar property.
		/// </summary>
		public Mapping<T> Scalar<V>(IEntityProperty<V> property, string field)
		{
			return this.Scalar<V>(property, context =>
				{
					if (context.Direction == MappingDirection.Inbound)
						context.Assign(context.GetField<V>(field));
					else
						context.SetField(field, context.AssignedValue);
				}
			);
		}

		/// <summary>
		/// Activates a function when a scalar property needs mapping.
		/// </summary>
		public Mapping<T> Scalar<V>(IEntityProperty<V> property, Action<MappingContext<V>> function)
		{
			this.SubMappings[property] = new Mapping<V>(this.EntitySpace, this.Direction) { MappingFunction = function };
			return this;
		}

		public Mapping<T> Collection<V>(ICollectionProperty<V> collection, string dataSet, string field)
		{
			return this.Collection<V>(collection, dataSet, context =>
			{
				if (context.Direction == MappingDirection.Inbound)
					context.Assign(context.GetField<V>(field));
				else
					context.SetField(field, context.AssignedValue);
			});
		}

		public Mapping<T> Collection<V>(ICollectionProperty<V> collection, string dataSet, Action<CollectionMappingContext<T,V>> function)
		{
			var collectionMapping = new Mapping<ICollection<V>>(this.EntitySpace, this.Direction) { DataSet = dataSet };

			collectionMapping.SubMappings[collection.Value] = new Mapping<V>(this.EntitySpace, this.Direction)
			{
				MappingFunction = function
			};

			this.SubMappings[collection] = collectionMapping;

			return this;
		}

		public Mapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string dataSet, string key, string value)
		{
			return this.Dictionary<K, V>(dictionary, dataSet,
				context =>
				{
					if (context.Direction == MappingDirection.Inbound)
						context.Assign(context.GetField<K>(key));
					else
						context.SetField(key, context.AssignedValue);
				},
				context =>
				{
					if (context.Direction == MappingDirection.Inbound)
						context.Assign(context.GetField<K>(value));
					else
						context.SetField(value, context.AssignedValue);
				}
			);
		}

		public Mapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string dataSet, Action<CollectionMappingContext<T,K>> key, string value)
		{
			return this.Dictionary<K, V>(dictionary, dataSet,
				key,
				context => context.Assign(context.GetField<V>(value))
			);
		}

		public Mapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string dataSet, string key, Action<CollectionMappingContext<T,V>> value)
		{
			return this.Dictionary<K, V>(dictionary, dataSet,
				context => context.Assign(context.GetField<K>(key)),
				value
			);
		}

		public Mapping<T> Dictionary<K, V>(IDictionaryProperty<K, V> dictionary, string dataSet, Action<CollectionMappingContext<T,K>> key, Action<CollectionMappingContext<T,V>> value)
		{
			var dictMapping = new Mapping<IDictionary<K, V>>(this.EntitySpace, this.Direction) { DataSet = dataSet, };

			dictMapping.SubMappings[dictionary.Key] = new Mapping<K>(this.EntitySpace, this.Direction)
			{
				MappingFunction = key
			};

			dictMapping.SubMappings[dictionary.Value] = new Mapping<V>(this.EntitySpace, this.Direction)
			{
				MappingFunction = value
			};

			this.SubMappings[dictionary] = dictMapping;

			return this;
		}
	}

	public interface IMappingContext : IMapping
	{
		PersistenceConnection Connection { get; }

		object AssignedValue { get; }
		V GetField<V>(string field, Func<object,V> convertFunction = null);
		IMappingContext SetField(string field, object value);
		IMappingContext Assign(object value);
	}

	public class MappingContext<T> : Mapping<T>, IMappingContext
	{
		public PersistenceConnection Connection { get; private set; }

		internal MappingContext(Mapping<T> mapping, MappingDirection dir, PersistenceConnection connection) : base(mapping.EntitySpace, dir)
		{
			this.Connection = connection;
			this.DataSet = mapping.DataSet;
			this.MappingFunction = mapping.MappingFunction;
			foreach (var sub in mapping.SubMappings)
			{
				this.SubMappings.Add(sub.Key, sub.Key.CreateContext(sub.Value, dir, connection));
			}
		}

		public T AssignedValue { get; private set; }

		public V GetField<V>(string field, Func<object, V> convertFunction = null)
		{
			throw new NotImplementedException();
		}

		public MappingContext<T> SetField(string field, object value)
		{
			if (this.Direction != MappingDirection.Outbound)
				throw new InvalidOperationException("Cannot set data set fields during an inbound mapping operation.");

			throw new NotImplementedException();
			return this;
		}

		public MappingContext<T> Assign(object rawValue)
		{
			if (this.Direction != MappingDirection.Inbound)
				throw new InvalidOperationException("Cannot assign entity property values during an inbound mapping operation.");

			throw new NotImplementedException();
			return this;
		}

		public MappingContext<T> Assign(T value)
		{
			if (this.Direction != MappingDirection.Inbound)
				throw new InvalidOperationException("Cannot assign entity property values during an inbound mapping operation.");

			throw new NotImplementedException();
			return this;
		}

		#region Explicit

		IMappingContext IMappingContext.Assign(object value)
		{
			return this.Assign(value);
		}

		IMappingContext IMappingContext.SetField(string field, object value)
		{
			return this.SetField(field, value);
		}

		object IMappingContext.AssignedValue
		{
			get { return this.AssignedValue; }
		}

		#endregion
	}

	public class CollectionMappingContext<T, V> : MappingContext<ICollection<V>>
	{
		public T CollectionParent;
		public EntityProperty<T, V> ValueProperty;

		internal CollectionMappingContext(Mapping<ICollection<V>> mapping, MappingDirection dir, PersistenceConnection connection)
			: base(mapping, dir, connection)
		{
		}

		public MappingContext<V> Value
		{
			get { return (MappingContext<V>)this.SubMappings[ValueProperty]; }
		}
	}

	public class DictionaryMappingContext<T, K, V> : MappingContext<IDictionary<K,V>>
	{
		public T CollectionParent;
		public EntityProperty<T, K> KeyProperty;
		public EntityProperty<T, V> ValueProperty;

		internal DictionaryMappingContext(Mapping<IDictionary<K, V>> mapping, MappingDirection dir, PersistenceConnection connection)
			: base(mapping, dir, connection)
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
