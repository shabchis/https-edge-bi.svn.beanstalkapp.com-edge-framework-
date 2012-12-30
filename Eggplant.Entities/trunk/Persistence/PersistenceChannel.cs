using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Persistence
{
	public abstract class PersistenceChannel: IDisposable
	{
		public abstract object GetField(string field);
		public abstract void SetField(string field, object value);
		public abstract void Dispose();
	}
}
