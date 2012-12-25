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
	public abstract class Mapping<T> : IMapping
	{
		internal Mapping(IMapping parentMapping, EntitySpace space)
		{
			this.ParentMapping = parentMapping;
			this.EntitySpace = parentMapping == null ? space : parentMapping.EntitySpace;
			this.EntityDefinition = space.GetDefinition<T>();
			this.SubMappings = new List<IMapping>();
		}

		// ===================================
		// Properties

		public EntitySpace EntitySpace { get; private set; }
		public EntityDefinition<T> EntityDefinition { get; private set; }
		public IMapping ParentMapping { get; private set; }
		public IList<IMapping> SubMappings { get; private set; }

		// ===================================
		// Internal

		// ===================================
		// Inline definition helpers

		/// <summary>
		/// Inherits all mappings from the base base mapping.
		/// </summary>
		public Mapping<T> Inherit(IMapping baseMapping)
		{
			// Add any submappings from base that are not already defined here
			((List<IMapping>)this.SubMappings).InsertRange(0, baseMapping.SubMappings.Where(m => !this.SubMappings.Any(s => s.Equals(m))));
			return this;
		}

		public Mapping<T> Set(Func<MappingContext<T>, T> function)
		{
			return this.Do(new Action<MappingContext<T>>(context =>
			{

				if (context.Direction == MappingDirection.Outbound)
					throw new MappingException("Cannot perform a Set() mapping in an outbound mapping.");

				context.SetTarget(function(context));
			}));

		}

		public Mapping<T> Do(Action<MappingContext<T>> action)
		{
			this.SubMappings.Add(new ActionMapping<T>(this) { Action = action });
			return this;
		}

		public Mapping<T> Map<V>(string variable, Action<VariableMapping<V>> init)
		{
			var submapping = new VariableMapping<V>(this) { Variable = variable };
			this.SubMappings.Add(submapping);
			init(submapping);
			return this;
		}

		public Mapping<T> Map<V>(string variable, string field)
		{
			return this.Map<V>(variable, mapping => mapping
				.Set(context =>
					context.Direction == MappingDirection.Inbound ?
						context.GetField<V>(field) :
						context.GetVariable<V>(variable)
				)
			);
		}

		public Mapping<T> Map<V>(IEntityProperty<V> property, Action<PropertyMapping<V>> init)
		{
			var submapping = new PropertyMapping<V>(this) { Property = property };
			this.SubMappings.Add(submapping);
			init(submapping);
			return this;
		}

		public Mapping<T> Map<V>(IEntityProperty<V> property, string field)
		{
			return this.Map<V>(property, mapping => mapping
				.Set(context =>
					context.Direction == MappingDirection.Inbound ?
						context.GetField<V>(field) :
						mapping.Property.GetValue(context.Target)
				)
			);
		}


		public Mapping<T> Subquery<V>(string subqueryName, Action<SubqueryMapping<V>> init)
		{
			var submapping = new SubqueryMapping<V>(this) { SubqueryName = subqueryName };
			this.SubMappings.Add(submapping);
			init(submapping);
			return this;
		}

		public Mapping<T> Subquery(string subqueryName, Action<SubqueryMapping<object>> init)
		{
			return this.Subquery<object>(subqueryName, init);
		}

		public Mapping<T> Inline<V>(Action<InlineMapping<V>> init)
		{
			var inlineMapping = new InlineMapping<V>(this);
			this.SubMappings.Add(inlineMapping);
			init(inlineMapping);
			return this;
		}

		public Mapping<T> Inline(Action<InlineMapping<object>> init)
		{
			return this.Inline<object>(init);
		}

		// Exxxtra sugar
		/*
		public Mapping<T> MapSubquery<K, V>(EntityProperty<T, Dictionary<K, V>> property, string subqueryName, Action<SubqueryDictionaryMapping<object>> init)
		{
			var mapping = this.Map<Dictionary<K, V>>(property, dictionary => dictionary
					.Set(context => new Dictionary<K,V>())
					.Subquery(subqueryName, subquery => subquery
						.Map<T>("parent", parent => parent
							.Map<long>(EdgeObject.Properties.GK, "GK")//,
							//resolve: IdentityResolve.ExistingOnly
						)
						.Map<ConnectionDefinition>("key", key => key
							.Map<int>(ConnectionDefinition.Properties.ID, "ConnectionID")
						)
						.Map<object>("value", value => value
							.Set(context => ConnectionDefinition.DeserializeValue<object>(
								context.GetField<string>("Value"),
								Type.GetType(context.GetField<string>("ActualValueType")))
							)
						)
						.Do(context => EdgeObject.Properties.Connections.GetValue(context.GetVar<EdgeObject>("parent")).Add(
								context.GetVar<ConnectionDefinition>("key"),
								context.GetVar<object>("value")
							)
						)
					)
				)
		}
		*/

		// ===================================
		// Other

		// Finds the nearest parent context
		public MappingContext<T> FromContext(MappingContext context)
		{
			while (context.ParentContext != null)
			{
				if (context.ParentContext.ActiveMapping == this)
					return (MappingContext<T>)context.ParentContext;
				else
					context = context.ParentContext;
			}

			throw new MappingException("Could not find the requested mapping target from the context specified.");
		}

		MappingContext IMapping.CreateContext(MappingContext baseContext)
		{
			return new MappingContext<T>(baseContext);
		}
	}
}
