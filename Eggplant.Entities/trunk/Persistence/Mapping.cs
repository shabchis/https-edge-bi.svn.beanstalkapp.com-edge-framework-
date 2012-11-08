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
			this.SubMappings = new List<IMapping>();
		}

		// ===================================
		// Properties

		public EntitySpace EntitySpace { get; private set; }
		public EntityDefinition<T> EntityDefinition { get; private set; }
		public Delegate MappingFunction { get; set; }
		public IEntityProperty<T> Property { get; internal set; }
		public List<IMapping> SubMappings { get; private set; }
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
			foreach (IMapping mapping in baseMapping.SubMappings)
			{
				IMapping existing = null;
				if (mapping.Property != null)
					existing = this.SubMappings.Single(subMapping => subMapping.Property == mapping.Property);

				if (existing != null)
				{
					// If we already have this property defined, either override it or ignore
					if (@override)
						this.SubMappings.Remove(existing);
					else
						continue;
				}

				this.SubMappings.Add(mapping);
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
		public Mapping<T> Map<V>(IEntityProperty<V> property, string field)
		{
			return this.Map<V>(property, context =>
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
		public Mapping<T> Map<V>(IEntityProperty<V> property, Action<MappingContext<V>> function)
		{
			this.SubMappings.Add(new Mapping<V>(this.EntitySpace) { Property = property, MappingFunction = function });
			return this;
		}

		public MappingContext<T> CreateContext(QueryBase query, MappingDirection dir)
		{
			return new MappingContext<T>(query, this, dir);
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

		IMappingContext IMapping.CreateContext(QueryBase query, MappingDirection dir)
		{
			return this.CreateContext(query, dir);
		}

		#endregion
	}
}
