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
		public IMapping BaseMapping { get; private set; }
		public IList<IMapping> SubMappings { get; private set; }
		public IdentityDefinition CacheIdentity { get; set; }

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
			this.BaseMapping = baseMapping;
			return this;
		}

		public Mapping<T> Identity(IdentityDefinition identityDefinition)
		{
			this.CacheIdentity = identityDefinition;
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

		public Mapping<T> Type(string field)
		{
			return this.Type(context => context.GetField<Type>(field, t => {
				Type type = null;
				if (t != null)
				{
					if (t is string && !String.IsNullOrWhiteSpace((string)t))
						type = System.Type.GetType((string)t);
					if (type == null)
						throw new MappingException(String.Format("Type information cannot be retrieved from the field '{0}' with the value is '{1}'.", field, t == null ? null : t));
				}
				return type;
			}));
		}

		public Mapping<T> Type(Func<MappingContext<T>, Type> function)
		{
			return this.Do(new Action<MappingContext<T>>(context =>
			{
				context.TargetType = function(context);
			}));

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
				.Do(context =>
				{
					if (context.HasField(field))
						context.Target = context.GetField<V>(field);
				})
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
				.Do(context =>
				{
					if (context.HasField(field))
						context.Target = context.GetField<V>(field);
				})
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

		/// <summary>
		/// Import another mapping's settings
		/// </summary>
		public Mapping<T> UseMapping(Mapping<T> mapping)
		{
			this.BaseMapping = mapping.BaseMapping;
			this.CacheIdentity = mapping.CacheIdentity;
			((List<IMapping>)this.SubMappings).AddRange(mapping.SubMappings);

			return this;
		}

		// ===================================
		// Other

		// Finds the nearest parent context
		public T FromContext(MappingContext context)
		{
			while (context.ParentContext != null)
			{
				if (context.ParentContext.CurrentMapping == this)
					return ((MappingContext<T>)context.ParentContext).Target;
				else
					context = context.ParentContext;
			}

			throw new MappingException("Could not find the requested mapping target from context.");
		}

		MappingContext IMapping.CreateContext(MappingContext parentContext)
		{
			return new MappingContext<T>(parentContext, this);
		}

		MappingContext IMapping.CreateContext(PersistenceAdapter adapter, Subquery subquery)
		{
			return new MappingContext<T>(adapter, subquery, this);
		}

		// ===================================
		// Apply

		void IMapping.Apply(MappingContext context)
		{
			this.Apply((MappingContext<T>)context);
		}

		public virtual void Apply(MappingContext<T> context)
		{
			((IMapping)this).InnerApply(context);
		}

		IEnumerable<IMapping> IMapping.GetAllMappings(bool includeBase, Type untilBase)
		{
			if (untilBase == typeof(T))
				yield break;

			if (includeBase && this.BaseMapping != null)
			{
				foreach (IMapping mapping in this.BaseMapping.GetAllMappings(true, untilBase))
					yield return mapping;

				foreach (IMapping mapping in this.BaseMapping.SubMappings)
					yield return mapping;
			}

			foreach (IMapping mapping in this.SubMappings)
				yield return mapping;
		}

		void IMapping.InnerApply(MappingContext c)
		{
			var context = (MappingContext<T>) c;

			// .......................
			// GET ALL MAPPINGS
			
			List<IMapping> mappingsToApply = null;

			foreach (IMapping mapping in ((IMapping)this).GetAllMappings(includeBase: true))
			{
				if (mappingsToApply == null)
					mappingsToApply = new List<IMapping>();

				mappingsToApply.Add(mapping);
			}

			// .......................
			// APPLY CHILD MAPPINGS

			Dictionary<IEntityProperty, object> propertyValues = null;
			bool hasChildMappings = false;

			// Apply current mappings
			for (int i = 0; i < mappingsToApply.Count; i++ )
			{
				IMapping mapping = mappingsToApply[i];

				// Ignore subquery mappings that aren't the current mapping
				if (mapping is ISubqueryMapping && context.CurrentSubquery.Mapping != mapping)
					continue;

				MappingContext currentContext = mapping is IChildMapping ?
					mapping.CreateContext(context) :
					context;

				mapping.Apply(currentContext);
				if (context.DoBreak)
					break;

				if (mapping is IChildMapping)
				{
					hasChildMappings = true;

					// If a target has been specified, save it
					if (currentContext.IsTargetSet)
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

				// There might be derived mappings at this point, find them
				if (i == mappingsToApply.Count - 1 && context.TargetType != null)
				{
					IEntityDefinition definition = this.EntitySpace.GetDefinition(context.TargetType);
					if (definition != null)
					{
						// TODO: better way to indicate which derived mapping to use - possibly by name
						IMapping derived = null;
						foreach (IMapping d in definition.Mappings.Where(m => m.BaseMapping == this))
						{
							derived = d;
							break;
						}

						// Get all inherit
						if (derived != null)
							mappingsToApply.AddRange(derived.GetAllMappings(includeBase: true, untilBase: typeof(T)));
					}
				}
			}

			// .......................
			// GET FROM CACHE

			if (this.EntitySpace.GetDefinition<T>() != null && this.CacheIdentity != null && (!context.IsTargetSet || context.Target != null))
			{
				var id = this.CacheIdentity.IdentityFrom(propertyValues);
				//context.Target = context.Cache.Get<T>(this.CacheIdentity, this.CacheIdentity.IdentityFrom(propertyValues), context.Target);
			}

			// .......................
			// INSTANTIATION

			// Instantiate target if it hasn't been specifically applied and there are sub mappings that have executed
			if (!context.IsTargetSet && hasChildMappings && (context.TargetType != null || typeof(T) != typeof(object)))
			{
				context.Target = context.TargetType != null ?
					(T)Activator.CreateInstance(context.TargetType) :
					Activator.CreateInstance<T>();
			}

			// .......................
			// APPLY VALUES

			// Apply property values
			if (propertyValues != null)
			{
				foreach (var propVal in propertyValues)
					propVal.Key.SetValue(context.Target, propVal.Value);
			}

			// .......................
			// UPDATE CACHE

			//if (this.EntitySpace.IsDefined<T>() && this.CacheIdentity != null && context.Target != null)
			//	context.Cache.Update<T>(this.CacheIdentity, context.Target);
		}
	}
}
