using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Cache;

namespace Eggplant.Entities.Persistence
{
	public abstract class PersistenceAdapter: IDisposable
	{
		public PersistenceAction Action { get; private set; }
		public MappingDirection MappingDirection { get; private set; }

		public PersistenceAdapter(PersistenceAction action, MappingDirection mappingDirection)
		{
			this.Action = action;
			this.MappingDirection = mappingDirection;
		}

		public abstract bool HasField(string field);
		public abstract object GetField(string field);
		public abstract void SetField(string field, object value);
		public abstract void Dispose();
		public abstract bool NextResultSet();
		public abstract bool NextResult();
	}

	public enum PersistenceAdapterPurpose
	{
		Results,
		Parameters
	}
}
