using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Cache;

namespace Eggplant.Entities.Persistence
{
	/// <summary>
	/// Interacts and executes with the persistence store using the action.
	/// </summary>
	public abstract class PersistenceAdapter: IDisposable
	{
		public PersistenceConnection Connection { get; private set; }
		public PersistenceCommand Command { get; private set; }
		//public Action InboundRowReceived { get; set; }

		protected PersistenceAdapter(PersistenceConnection connection, PersistenceCommand command)
		{
			this.Connection = connection;
			this.Command = command;
		}

		public abstract bool IsReusable { get; }

		public abstract void Begin();
		public abstract void End();

		public abstract bool HasOutboundField(string field);
		public abstract object GetOutboundField(string field);
		public abstract void SetOutboundField(string field, object value);
		public abstract void NewOutboundRow();
		public abstract bool SubmitOutboundRow();

		public abstract bool NextInboundSet();
		public abstract bool NextInboundRow();
		public abstract int InboundSetIndex { get; }
		public abstract bool HasInboundField(string field);
		public abstract object GetInboundField(string field);
		//public abstract void SetInboundField(string field, object value);

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			this.End();
		}

		#endregion
	}
}
