using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Eggplant2.Model
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
					if (entityProperty.TargetMember == null)
					{
						// Try to get target member manually
						MemberInfo[] potentialMembers = typeof(T).GetMember(entityProperty.Name, MemberTypes.Property | MemberTypes.Field, BindingFlags.Public | BindingFlags.Instance);
						if (potentialMembers.Length > 0)
							entityProperty.TargetMember = potentialMembers[0];
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
