using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Edge.Data.Objects;
using System.Reflection;

namespace Edge.Data.Objects.Reflection
{
	public abstract class MappedType
	{
		static Dictionary<Type, MappedTypeMetadata> _cache = new Dictionary<Type, MappedTypeMetadata>();

		internal MappedTypeMetadata RegisterType(Type type)
		{
			MappedTypeMetadata metadata;
			lock (_cache)
			{
				if (Attribute.IsDefined(type, typeof(TypeIDAttribute)))
				{
					// Get type ID
					int typeid = ((TypeIDAttribute)Attribute.GetCustomAttribute(type, typeof(TypeIDAttribute))).TypeID;

					List<MappedFieldMetadata> fields = new List<MappedFieldMetadata>();

					// Get field indexes
					foreach (FieldInfo field in this.GetType().GetFields())
					{
						if (Attribute.IsDefined(field, typeof(FieldIndexAttribute)))
						{
							int columnIndex = ((FieldIndexAttribute)Attribute.GetCustomAttribute(field, typeof(FieldIndexAttribute))).ColumnIndex;
							fields.Add(new MappedFieldMetadata() { ColumnIndex = columnIndex, FieldInfo = field });
						}
					}

					_cache[type] = metadata = new MappedTypeMetadata() { TypeID = typeid, Fields = fields.ToArray() };
				}
				else
					throw new Exception("TypeID attribute is not defined on this class.");
			}

			return metadata;
		}

		internal int TypeID
		{
			get
			{
				Type type = this.GetType();
				MappedTypeMetadata metadata;
				if (!_cache.TryGetValue(type, out metadata))
					metadata = RegisterType(type);
				return metadata.TypeID;
			}
		}

		internal Dictionary<MappedFieldMetadata, object> GetFieldValues()
		{
			Type type = this.GetType();
			MappedTypeMetadata metadata;
			if (!_cache.TryGetValue(type, out metadata))
				metadata = RegisterType(type);

			var values = new Dictionary<MappedFieldMetadata, object>();
			foreach (MappedFieldMetadata field in metadata.Fields)
				values[field] = field.FieldInfo.GetValue(this);

			return values;
		}
	}

	class MappedTypeMetadata
	{
		public int TypeID;
		public MappedFieldMetadata[] Fields;
	}

	class MappedFieldMetadata
	{
		public int ColumnIndex;
		public FieldInfo FieldInfo;
	}

	class TypeIDAttribute : Attribute
	{
		internal int TypeID;
		public TypeIDAttribute(int typeID)
		{
			TypeID = typeID;
		}
	}

	class FieldIndexAttribute : Attribute
	{
		internal int ColumnIndex;
		public FieldIndexAttribute(int columnIndex)
		{
			ColumnIndex = columnIndex;
		}
	}
}
