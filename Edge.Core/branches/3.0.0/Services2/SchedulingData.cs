using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Edge.Core.Services2
{
	public class SchedulingRule
	{
	}

	[Serializable]
	public class SchedulingData: ISerializable
	{
		#region ISerializable Members

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			throw new NotImplementedException();
		}

		private SchedulingData(SerializationInfo info, StreamingContext context)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
