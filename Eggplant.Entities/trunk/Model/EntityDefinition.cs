using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Eggplant.Entities.Model
{
	public interface IEntityDefinition
	{
		IEntityDefinition BaseDefinition { get; }
		Type TargetType { get; }
		IDictionary<string,IEntityProperty> Properties { get; }
	}

	public class EntityDefinition<T> : IEntityDefinition
	{
		public readonly Type TargetType = typeof(T);
		public Dictionary<string,IEntityProperty> Properties { get; private set; }
		public Func<T, object> Identity;

		public IEntityDefinition BaseDefinition { get; private set; }

		public EntityDefinition(IEntityDefinition baseDefinition = null, Type fromReflection = null)
		{
			this.Properties = new Dictionary<string,IEntityProperty>();
			this.BaseDefinition = baseDefinition;

			if (fromReflection != null)
			{
				FieldInfo[] fields = fromReflection.GetFields(BindingFlags.Static | BindingFlags.Public);
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
								if (entityProperty.Getter == null ) entityProperty.Getter = new Func<object, object>(entity => fieldInfo.GetValue(entity));
								if (entityProperty.Setter == null) entityProperty.Setter = new Action<object, object>((entity, value) => fieldInfo.SetValue(entity, value));
							}
							if (bestMatchMember is PropertyInfo)
							{
								var propertyInfo = (PropertyInfo)bestMatchMember;
								if (entityProperty.Getter == null) entityProperty.Getter = new Func<object, object>(entity => propertyInfo.GetValue(entity,null));
								if (entityProperty.Setter == null) entityProperty.Setter = new Action<object, object>((entity, value) => propertyInfo.SetValue(entity, value, null));
							}
						}
					}

					this.Properties.Add(info.Name, entityProperty);
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

		#endregion
	}

}
