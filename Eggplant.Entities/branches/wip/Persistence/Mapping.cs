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
		public MappingDirection Direction { get; set; }

		private MappingDirection _shorthandDirection = MappingDirection.Inbound | MappingDirection.Outbound;

		// ===================================
		// General


	
		// ===================================
		// Inline definition helpers

		/// <summary>
		/// Defines mappings for inbound use only.
		/// </summary>
		public Mapping<T> WhenInbound(Action<Mapping<T>> inboundOnlyMappings)
		{
			MappingDirection prev = _shorthandDirection;
			_shorthandDirection = MappingDirection.Inbound;
			inboundOnlyMappings(this);
			_shorthandDirection = prev;
			return this;
		}

		/// <summary>
		/// Defines mappings for outbound use only.
		/// </summary>
		public Mapping<T> WhenOutbound(Action<Mapping<T>> outboundOnlyMappings)
		{
			MappingDirection prev = _shorthandDirection;
			_shorthandDirection = MappingDirection.Outbound;
			outboundOnlyMappings(this);
			_shorthandDirection = prev;
			return this;
		}

		/// <summary>
		/// When running, will use the base mapping anything defined directly on this.
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
				context.MappedValue = function(context);
			}));
		}

		public Mapping<T> Do(Action<MappingContext<T>> function)
		{
			this.SubMappings.Add(new FunctionMapping<T>(this) { Direction = _shorthandDirection, Function = function });
			return this;
		}

		public Mapping<T> Type(string field)
		{
			return this.Type(context =>
				{
					if (context.Direction == MappingDirection.Inbound)
					{
						return context.GetField<Type>(field, t =>
						{
							Type type = null;
							if (t != null)
							{
								if (t is string && !String.IsNullOrWhiteSpace((string)t))
									type = System.Type.GetType((string)t);
								if (type == null)
									throw new MappingException(String.Format("Type information cannot be retrieved from the field '{0}' with the value is '{1}'.", field, t == null ? null : t));
							}
							return type;
						});
					}
					else if (context.Direction == MappingDirection.Outbound)
					{
						Type t = context.MappedValue.GetType();
						context.SetField(field,t.AssemblyQualifiedName);
						return t;
					}
					else
						throw new InvalidOperationException();
				});
		}

		public Mapping<T> Type(Func<MappingContext<T>, Type> function)
		{
			return this.Do(new Action<MappingContext<T>>(context =>
			{
				if (context.Direction == MappingDirection.Inbound)
					context.MappedValueType = function(context);
				else
					function(context);
			}));

		}

		public Mapping<T> Map<V>(string variable, Action<VariableMapping<V>> init)
		{
			var submapping = new VariableMapping<V>(this) {Direction = _shorthandDirection, Variable = variable };
			this.SubMappings.Add(submapping);
			init(submapping);
			return this;
		}

		public Mapping<T> Map<V>(string variable, string field)
		{
			return this.Map<V>(variable, mapping => mapping
				.Do(context =>
				{
					if (!context.HasField(field))
						return;
					if (context.Direction == MappingDirection.Inbound)
						context.MappedValue = context.GetField<V>(field);
					else if (context.Direction == MappingDirection.Outbound)
						context.SetField(field, context.MappedValue);
					else
						throw new InvalidOperationException();
				})
			);
		}

		public Mapping<T> Map<V>(IEntityProperty<V> property, Action<PropertyMapping<V>> init)
		{
			var submapping = new PropertyMapping<V>(this) { Direction = _shorthandDirection, Property = property };
			this.SubMappings.Add(submapping);
			init(submapping);
			return this;
		}

		public Mapping<T> Map<V>(IEntityProperty<V> property, string field = null, string param = null, Func<object, V> convertIn = null, Func<V, object> convertOut = null)
		{
			return this.Map<V>(property, mapping => mapping
				.Do(context =>
				{
					if (!context.HasField(field))
						return;
					if (context.Direction == MappingDirection.Inbound)
						context.MappedValue = context.GetField<V>(field, convertIn);
					else if (context.Direction == MappingDirection.Outbound)
						context.SetField(field, context.MappedValue, convertOut);
				})
			);
		}

		public Mapping<T> Subquery<V>(string subqueryName, Action<SubqueryMapping<V>> init)
		{
			var submapping = new SubqueryMapping<V>(this) { Direction = _shorthandDirection, SubqueryName = subqueryName };
			this.SubMappings.Add(submapping);
			init(submapping);

			return this;
		}

		public Mapping<T> Subquery(string subqueryName, Action<SubqueryMapping<object>> init)
		{
			return this.Subquery<object>(subqueryName, init);
		}


		/// <summary>
		/// Import another mapping's settings
		/// </summary>
		public Mapping<T> UseMapping(Mapping<T> mapping)
		{
			this.BaseMapping = mapping.BaseMapping;

			if (this.CacheIdentity == null)
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
					return ((MappingContext<T>)context.ParentContext).MappedValue;
				else
					context = context.ParentContext;
			}

			throw new MappingException("Could not find the requested mapping target from context.");
		}

		MappingContext IMapping.CreateContext(MappingContext parentContext)
		{
			return new MappingContext<T>(parentContext, this);
		}

		MappingContext IMapping.CreateContext(PersistenceAdapter adapter, Subquery subquery, MappingDirection direction)
		{
			return new MappingContext<T>(adapter, subquery, this, direction);
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
			// GET ALL MAPPINGS FOR CURRENT DIRECTION

			List<IMapping> mappingsToApply = ((IMapping)this)
				.GetAllMappings(includeBase: true)
				.Where(mapping => mapping.Direction == c.Direction)				
				.ToList();

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
					// If a target has been specified, save it
					if (currentContext.IsMappedValueSet)
					{
						hasChildMappings = true;

						if (mapping is IPropertyMapping)
						{
							if (propertyValues == null)
								propertyValues = new Dictionary<IEntityProperty, object>();

							var propertyMapping = (IPropertyMapping)mapping;
							propertyValues.Add(propertyMapping.Property, currentContext.MappedValue);
						}
						else if (mapping is IVariableMapping)
						{
							var variableMapping = (IVariableMapping)mapping;
							context.SetVariable(variableMapping.Variable, currentContext.MappedValue);
						}
					}
				}

				// There might be derived mappings at this point, find them
				if (i == mappingsToApply.Count - 1 && context.MappedValueType != null)
				{
					IEntityDefinition definition = this.EntitySpace.GetDefinition(context.MappedValueType);
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

			bool isEntity = this.EntitySpace.GetDefinition<T>() != null;
			bool shouldCache = isEntity && this.CacheIdentity != null && propertyValues != null && this.CacheIdentity.HasValidValues(propertyValues);

			if (shouldCache)
			{
				Identity cacheId = this.CacheIdentity.IdentityFromValues(propertyValues);

				object val = context.Cache.Get<T>(cacheId);
				if (val != null)
				{
					// Update cache
					context.MappedValue = (T)val;
					context.Cache.Update(this.CacheIdentity, propertyValues);
					shouldCache = false;
				}
			}

			// .......................
			// INSTANTIATION

			// Instantiate target if it hasn't been specifically applied and there are sub mappings that have executed
			if (!context.IsMappedValueSet && hasChildMappings)
			{
				// Instantiate a new target
				context.MappedValue = context.MappedValueType != null ?
					(T)Activator.CreateInstance(context.MappedValueType) :
					Activator.CreateInstance<T>();
			}

			// .......................
			// PUT IN CACHE

			if (context.IsMappedValueSet && propertyValues != null)
			{
				// Apply properties
				foreach (var propVal in propertyValues)
					propVal.Key.SetValue(context.MappedValue, propVal.Value);

				// Add to cache
				if (shouldCache)
					context.Cache.Put<T>(this.CacheIdentity, context.MappedValue, propertyValues.Keys.ToArray());
			}
		}
	}
}
