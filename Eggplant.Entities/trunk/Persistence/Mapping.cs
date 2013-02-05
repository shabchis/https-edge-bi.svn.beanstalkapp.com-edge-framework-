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
			this.EntityDefinition = this.EntitySpace.GetDefinition<T>();
			this.SubMappings = new List<IMapping>();
		}

		// ===================================
		// Properties

		public EntitySpace EntitySpace { get; private set; }
		public EntityDefinition<T> EntityDefinition { get; private set; }
		public IMapping ParentMapping { get; private set; }
		public IList<IMapping> SubMappings { get; private set; }
		public IdentityDefinition CacheIdentity { get; private set; }

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
				context.Target = function(context);
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
				.Set(context => context.GetField<V>(field)
					//context.Direction == MappingDirection.Inbound ?
					//	context.GetField<V>(field) :
					//	context.GetVariable<V>(variable)
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
				.Set(context => context.GetField<V>(field)
					//context.Direction == MappingDirection.Inbound ?
					//	context.GetField<V>(field) :
					//	mapping.Property.GetValue(context.Target)
				)
			);
		}


		public Mapping<T> Subquery<V>(string subqueryName, Action<SubqueryMapping<V>> init)
		{
			var submapping = new SubqueryMapping<V>(this) { SubqueryName = subqueryName };
			this.SubMappings.Add(submapping);
			init(submapping);

			IMapping[] submappings = submapping.SubMappings.AsEnumerable().ToArray();
			submapping.SubMappings.Clear();

			// After initialization, convert the subquery so that every submapping is actually applied to each child row
			submapping.Do(context=> {
				do
				{
					foreach (IMapping mapping in submappings)
					{
						mapping.Apply(context);
					}
				}
				while (context.Adapter.Read());
			});
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
		public T FromContext(MappingContext context)
		{
			while (context.ParentContext != null)
			{
				if (context.ParentContext.ActiveMapping == this)
					return ((MappingContext<T>)context.ParentContext).Target;
				else
					context = context.ParentContext;
			}

			throw new MappingException("Could not find the requested mapping target from context.");
		}

		MappingContext IMapping.CreateContext(MappingContext parentContext)
		{
			return new MappingContext<T>(parentContext);
		}

		MappingContext IMapping.CreateContext(PersistenceAdapter adapter)
		{
			return new MappingContext<T>(adapter);
		}

		// ===================================
		// Apply

		void IMapping.Apply(MappingContext context)
		{
			this.Apply((MappingContext<T>)context);
		}

		public virtual void Apply(MappingContext<T> context)
		{
			this.ApplyInner(context);
		}

		protected void ApplyInner(MappingContext<T> context)
		{
			Dictionary<IEntityProperty, object> propertyValues = null;

			// Apply child mappings
			foreach (IMapping mapping in this.SubMappings)
			{
				MappingContext currentContext = mapping is IChildMapping ?
					mapping.CreateContext(context) :
					context;

				mapping.Apply(currentContext);
				if (context.DoBreak)
					break;

				if (mapping is IChildMapping)
				{
					if (mapping is IPropertyMapping)
					{
						if (propertyValues == null)
							propertyValues = new Dictionary<IEntityProperty, object>();

						var propertyMapping = (IPropertyMapping)mapping;
						propertyValues.Add(propertyMapping.Property, currentContext.Target);
					}
					else if (mapping is IVariableMapping)
					{
						var variableMapping = (IVariableMapping)mapping;
						context.SetVariable(variableMapping.Variable, currentContext.Target);
					}
				}
			}

			// TODO: Cache the object (either retrieve a more complete cached object, or insert this into the cache)
			//if (this.EntitySpace.IsDefined<T>() && this.CacheIdentity != null && context.Target != null)
			//	context.Target = context.Cache.Get<T>(this.CacheIdentity, this.CacheIdentity.IdentityOf(context.Target));

			if (!context.IsTargetSet)
			{
				context.Target = Activator.CreateInstance<T>();
			}

			if (propertyValues != null)
			{
				foreach (var propVal in propertyValues)
					propVal.Key.SetValue(context.Target, propVal.Value);
			}
		}
	}
}
