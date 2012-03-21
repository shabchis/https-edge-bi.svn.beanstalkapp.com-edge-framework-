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
							int columnIndex = ((MappedObjectFieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(MappedObjectFieldIndexAttribute))).ColumnIndex;
							fields.Add(new MappedObjectField() { ColumnIndex = columnIndex, FieldInfo = field });
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
				if (metadata.TypeID == 0)
					return GetDynamicTypeID();
				else
					return metadata.TypeID;
			}
		}

		protected virtual int GetDynamicTypeID()
		{
			throw new NotImplementedException("GetDynamicTypeID must be overridden if the MappedObjectTypeID attribute is not defined on the class.");
		}

		public Dictionary<MappedObjectField, object> GetFieldValues()
		{
			MappedObjectType metadata = RegisterType();

			var values = new Dictionary<MappedObjectField, object>();
			foreach (MappedObjectField field in metadata.Fields)
				values[field] = field.FieldInfo.GetValue(this);

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
		public MappedObjectFieldIndexAttribute(int columnIndex)
		{
			ColumnIndex = columnIndex;
		}
	}
}
