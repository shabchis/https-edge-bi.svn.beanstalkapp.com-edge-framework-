using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Queries;

namespace Eggplant.Entities.Persistence
{
	/// <summary>
	/// When derived, represents a persistence store command definition.
	/// </summary>
	public abstract class PersistenceCommand
	{
		public readonly Dictionary<string, PersistenceParameter> Parameters = new Dictionary<string,PersistenceParameter>();

		public abstract bool IsAppendable { get; }

		/// <summary>
		/// Appends the contents of another command.
		/// </summary>
		public void Append(PersistenceCommand command)
		{
			OnAppend(command);

			// Append all parameters. This will cause an exception if a parameter with the same name already exists.
			foreach (PersistenceParameter param in command.Parameters.Values)
				this.Parameters.Add(param.Name, param.Clone());
		}

		protected abstract void OnAppend(PersistenceCommand command);
		
		/// <summary>
		/// Clones the command and all its contents.
		/// </summary>
		public abstract PersistenceCommand Clone();

		/// <summary>
		/// Gets an adapter that can be used to execute and interact with the results of this command.
		/// </summary>
		public abstract PersistenceAdapter GetAdapter(PersistenceConnection connection);
	}
}
