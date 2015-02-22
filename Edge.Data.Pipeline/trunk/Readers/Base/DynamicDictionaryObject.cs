using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Dynamic;
using System.Runtime.Serialization;

namespace Edge.Data.Pipeline
{
	[Serializable]
	public class DynamicDictionaryObject: DynamicObject, ISerializable
	{
		public bool CaseSensitive = true;
		public Dictionary<string, object> Values { get; private set; }

		public DynamicDictionaryObject()
		{
		}

		public override IEnumerable<string> GetDynamicMemberNames()
		{
			return base.GetDynamicMemberNames();
		}

		public override bool TryGetMember(GetMemberBinder binder, out object result)
		{
			string name = binder.Name;
			Values.TryGetValue(name, out result);
			return true; // always return true to avoid 'undefined member' exception
		}

		public override bool TrySetMember(SetMemberBinder binder, object value)
		{
			SetMember(binder.Name, value);
			return true;
		}

		private void SetMember(string name, object value)
		{
			if (Values == null)
				Values = this.CaseSensitive ?
					new Dictionary<string,object>():
					new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

			if (!SetMemberInternal(name, value))
				Values[name] = value;
		}

		protected virtual bool SetMemberInternal(string name, object value)
		{
			return false;
		}

		public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
		{
			if (this.Values == null || indexes.Length != 1 || !(indexes[0] is string))
			{
				result = null;
				return false;
			}

			string name = indexes[0] as string;
			return this.Values.TryGetValue(name, out result);
		}

		public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
		{
			if (indexes.Length != 1 || !(indexes[0] is string))
			{
				return false;
			}

			this.SetMember(indexes[0] as string, value);
			return true;
		}

		#region ISerializable Members

		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("Values", this.Values);
			info.AddValue("CaseSensitive", this.CaseSensitive);
		}

		private DynamicDictionaryObject(SerializationInfo info, StreamingContext context)
		{
			this.Values = (Dictionary<string, object>) info.GetValue("Values", typeof(Dictionary<string, object>));
			this.CaseSensitive = info.GetBoolean("CaseSensitive");
		}

		#endregion
	}
}
