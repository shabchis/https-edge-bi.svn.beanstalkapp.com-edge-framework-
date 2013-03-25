using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Queries;

namespace Eggplant.Entities.Persistence
{
	public abstract class PersistenceAction
	{
		Dictionary<string, PersistenceParameter> _appliedParameters = null;

		public PersistenceConnection Connection { get; internal set; }

		/// <summary>
		/// Appends the contents of another action.
		/// </summary>
		public abstract void Append(PersistenceAction action);
		
		/// <summary>
		/// Clones the action and all its contents.
		/// </summary>
		public abstract PersistenceAction Clone();

		/// <summary>
		/// 
		/// </summary>
		public abstract PersistenceAdapter GetAdapter(PersistenceAdapterPurpose purpose, MappingDirection mappingDirection);

		/// <summary>
		/// Applies the value and options of an action parameter to the underlying implementation.
		/// </summary>
		/// <param name="param"></param>
		public void ApplyParameter(PersistenceParameter param)
		{
			// Keep a copy for later reference
			param = param.Clone();

			// ...............................
			// Check for parameter conflicts
			if (_appliedParameters == null)
			{
				_appliedParameters = new Dictionary<string, PersistenceParameter>();
			}
			else
			{
				PersistenceParameter existing;
				if (_appliedParameters.TryGetValue(param.Name, out existing) && existing.Options != null)
				{
					if (param.Options == null)
					{
						if (existing.Options != null)
							param.Options = existing.Options.Clone();
					}
					else if (!Object.Equals(existing.Options, param.Options))
						throw new QueryTemplateException(String.Format("Persistence parameter conflict: '{0}' is declared more than once but with different options.", param.Name));
				}
			}

			_appliedParameters[param.Name] = param;
			OnApplyParameter(param);
		}

		protected abstract void OnApplyParameter(PersistenceParameter param);
	}
}
