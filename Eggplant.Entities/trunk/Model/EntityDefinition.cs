using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Eggplant.Entities.Persistence;

namespace Eggplant.Entities.Model
{
	public interface IEntityDefinition
	{
		IEntityDefinition BaseDefinition { get; }
		Type TargetType { get; }
		IDictionary<string,IEntityProperty> Properties { get; }
		IList<IdentityDefinition> Identities { get; }
		IList<IMapping> Mappings { get; }
	}

	public class EntityDefinition<T> : IEntityDefinition
	{
		public readonly Type TargetType = typeof(T);
		public Dictionary<string,IEntityProperty> Properties { get; private set; }
		public List<IdentityDefinition> Identities { get; private set; }
		public List<IMapping> Mappings { get; private set; }

		public IEntityDefinition BaseDefinition { get; private set; }

		public EntityDefinition(IEntityDefinition baseDefinition = null, bool fromReflection = false, Type reflectionSource = null)
		{
			this.BaseDefinition = baseDefinition;
			this.Properties = new Dictionary<string,IEntityProperty>();
			this.Mappings = new List<IMapping>();

			// Use reflection to find properties/mappings/identites if fromReflection is true or reflectionSource is specified
			fromReflection |= reflectionSource != null;
			if (!fromReflection)
				return;

			// Use either the specified reflection source or the type itself
			reflectionSource = reflectionSource ?? typeof(T);

			Type propContainer = reflectionSource.GetNestedType("Properties", BindingFlags.Static | BindingFlags.Public);
			if (propContainer != null)
			{
				FieldInfo[] fields = propContainer.GetFields(BindingFlags.Static | BindingFlags.Public);
				foreach (FieldInfo info in fields)
				{
					if (!typeof(IEntityProperty).IsAssignableFrom(info.FieldType))
						continue;

					var entityProperty = (IEntityProperty) info.GetValue(null);
					if (entityProperty.Getter == null || entityProperty.Setter == null)
					{
						// Try to get target member manually
						MemberInfo[] potentialMembers = typeof(T).GetMember(entityProperty.Name, MemberTypes.Property | MemberTypes.Field, BindingFlags.Public | BindingFlags.Instance);
						if (potentialMembers.Length > 0)
						{
							MemberInfo bestMatchMember = potentialMembers[0];

							if (bestMatchMember is FieldInfo)
							{
								var fieldInfo = (FieldInfo)bestMatchMember;
								if (entityProperty.Getter == null) entityProperty.Getter = new Func<object, object>(entity => fieldInfo.GetValue(entity));
								if (entityProperty.Setter == null) entityProperty.Setter = new Action<object, object>((entity, value) => fieldInfo.SetValue(entity, value));
							}
							if (bestMatchMember is PropertyInfo)
							{
								var propertyInfo = (PropertyInfo)bestMatchMember;
								if (entityProperty.Getter == null) entityProperty.Getter = new Func<object, object>(entity => propertyInfo.GetValue(entity, null));
								if (entityProperty.Setter == null) entityProperty.Setter = new Action<object, object>((entity, value) => propertyInfo.SetValue(entity, value, null));
							}
						}
						else
							throw new EntityDefinitionException(String.Format("Could not find a field or property named '{0}' in the type {1}.", entityProperty.Name, typeof(T).FullName));
					}

					this.Properties.Add(info.Name, entityProperty);
				}
			}

			Type mappingContainer = reflectionSource.GetNestedType("Mappings", BindingFlags.Static | BindingFlags.Public);
			if (mappingContainer != null)
			{
				FieldInfo[] fields = mappingContainer.GetFields(BindingFlags.Static | BindingFlags.Public);
				foreach (FieldInfo info in fields)
				{
					if (!typeof(Mapping<T>).IsAssignableFrom(info.FieldType))
						continue;

					this.Mappings.Add((IMapping)info.GetValue(null));
				}
			}
		}

		#region IEntityDefinition Members

		Type IEntityDefinition.TargetType
		{
			get { return this.TargetType; }
		}

		IDictionary<string, IEntityProperty> IEntityDefinition.Properties
		{
			get { return this.Properties; }
		}

		IList<IMapping> IEntityDefinition.Mappings
		{
			get { return this.Mappings; }
		}

		IList<IdentityDefinition> IEntityDefinition.Identities
		{
			get { return this.Identities; }
		}

		#endregion
	}

	[Serializable]
	public class EntityDefinitionException : Exception
	{
		public EntityDefinitionException() { }
		public EntityDefinitionException(string message) : base(message) { }
		public EntityDefinitionException(string message, Exception inner) : base(message, inner) { }
		protected EntityDefinitionException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context)
			: base(info, context) { }
	}
}
