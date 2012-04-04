using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects;
using System.Reflection;

namespace Edge.Data.Objects.Reflection
{
	public abstract class MappedObject
	{
		static Dictionary<Type, MappedObjectType> _cache = new Dictionary<Type, MappedObjectType>();

		public MappedObjectType RegisterType()
		{
			Type type = this.GetType();
			MappedObjectType metadata;

			// Check if type already registered
			if (!_cache.TryGetValue(type, out metadata))
			{
				int typeid = Attribute.IsDefined(type, typeof(MappedObjectTypeIDAttribute)) ?
					((MappedObjectTypeIDAttribute)Attribute.GetCustomAttribute(type, typeof(MappedObjectTypeIDAttribute))).TypeID :
					0;

				lock (_cache)
				{
					List<MappedObjectField> fields = new List<MappedObjectField>();

					// Get field indexes
					foreach (FieldInfo field in type.GetFields())
					{
						if (Attribute.IsDefined(field, typeof(MappedObjectFieldIndexAttribute)))
						{
							var attribute = (MappedObjectFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(MappedObjectFieldIndexAttribute));
							int columnIndex = attribute.ColumnIndex;
							string valueSourceName = attribute.ValueSource;

							// If a value source was specified, find a field, property that has a getter, or method with no params that returns a value
							MemberInfo valueSource = null;
							if (valueSourceName != null)
							{
								MemberInfo[] members = field.FieldType.GetMember(valueSourceName, BindingFlags.Public | BindingFlags.NonPublic);
								foreach (MemberInfo member in members)
								{
									if (
										member.MemberType == MemberTypes.Field ||
										(
											member.MemberType == MemberTypes.Property &&
											((PropertyInfo)member).CanRead
										) ||
										(
											member.MemberType == MemberTypes.Method &&
											((MethodInfo)member).ReturnType != typeof(void) &&
											((MethodInfo)member).GetParameters().Length == 0 &&
											!((MethodInfo)member).IsGenericMethod
										)
									)
									{
										valueSource = member;
										break;
									}
								}

							}

							fields.Add(new MappedObjectField() { ColumnIndex = columnIndex, FieldInfo = field, ValueSource = valueSource });
						}
					}

					_cache[type] = metadata = new MappedObjectType() { TypeID = typeid, Fields = fields.ToArray() };
				}
			}

			return metadata;
		}

		public int TypeID
		{
			get
			{
				MappedObjectType metadata = RegisterType();
				return metadata.TypeID;
			}
		}

		public Dictionary<MappedObjectField, object> GetFieldValues()
		{
			MappedObjectType metadata = RegisterType();

			var values = new Dictionary<MappedObjectField, object>();
			foreach (MappedObjectField field in metadata.Fields)
			{
				object value = field.FieldInfo.GetValue(this);
				if (field.ValueSource != null)
				{
					if (field.ValueSource is FieldInfo)
						value = ((FieldInfo)field.ValueSource).GetValue(value);
					else if (field.ValueSource is PropertyInfo)
						value = ((PropertyInfo)field.ValueSource).GetValue(value, null);
					else if (field.ValueSource is MethodInfo)
						value = ((MethodInfo)field.ValueSource).Invoke(value, null);
				}
				values[field] = value;
			}

			return values;
		}
	}

	public class MappedObjectType
	{
		public int TypeID;
		public MappedObjectField[] Fields;
	}

	public class MappedObjectField
	{
		public int ColumnIndex;
		public FieldInfo FieldInfo;
		public MemberInfo ValueSource;
	}

	public class MappedObjectTypeIDAttribute : Attribute
	{
		internal int TypeID;
		public MappedObjectTypeIDAttribute(int typeID)
		{
			TypeID = typeID;
		}
	}

	public class MappedObjectFieldIndexAttribute : Attribute
	{
		internal int ColumnIndex;
		public string ValueSource { get; set; }

		public MappedObjectFieldIndexAttribute(int columnIndex)
		{
			ColumnIndex = columnIndex;
		}
	}
}
