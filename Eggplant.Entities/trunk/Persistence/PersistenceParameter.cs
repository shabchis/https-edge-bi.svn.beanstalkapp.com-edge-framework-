using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Persistence
{
	public class PersistenceParameter
	{
		public string Name {get; set;}
		public object Value {get; set;}
		public PersistenceParameterOptions Options {get; set;}

		public PersistenceParameter(string name, object value = null, PersistenceParameterOptions options = null)
		{
			this.Name = name;
			this.Value = value;
			this.Options = options;
		}

		public PersistenceParameter Clone()
		{
			return new PersistenceParameter(this.Name, this.Value, this.Options != null ? this.Options.Clone() : null);
		}
	}

	public abstract class PersistenceParameterOptions
	{
		public abstract PersistenceParameterOptions Clone();
	}
}
