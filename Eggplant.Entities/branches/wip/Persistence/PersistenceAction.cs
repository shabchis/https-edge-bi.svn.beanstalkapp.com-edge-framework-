using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Queries;

namespace Eggplant.Entities.Persistence
{
	/// <summary>
	/// When derived, represents a persistence store -specific action definition.
	/// </summary>
	public abstract class PersistenceAction
	{
		public readonly Dictionary<string, PersistenceParameter> Parameters = new Dictionary<string,PersistenceParameter>();

		public abstract bool IsAppendable { get; }

		/// <summary>
		/// Appends the contents of another action.
		/// </summary>
		public void Append(PersistenceAction action)
		{
			OnAppend(action);

			// Append all parameters. This will cause an exception if a parameter with the same name already exists.
			foreach (PersistenceParameter param in action.Parameters.Values)
				this.Parameters.Add(param.Name, param.Clone());
		}

		protected abstract void OnAppend(PersistenceAction action);
		
		/// <summary>
		/// Clones the action and all its contents.
		/// </summary>
		public abstract PersistenceAction Clone();

		/// <summary>
		/// Gets an adapter that can be used to execute and interact with the results of this action.
		/// </summary>
		public abstract PersistenceAdapter GetAdapter(PersistenceConnection connection);
	}
}
