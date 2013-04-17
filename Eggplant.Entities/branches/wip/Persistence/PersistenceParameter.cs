using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Eggplant.Entities.Persistence
{
	public class PersistenceParameter
	{
		public string Name {get; set;}
		public object Value {get; set;}
		public MappingDirection Direction { get; set; }
		public PersistenceParameterOptions Options {get; set;}

		public PersistenceParameter(string name, object value = null, MappingDirection direction = MappingDirection.Outbound, PersistenceParameterOptions options = null)
		{
			this.Name = name;
			this.Value = value;
			this.Direction = direction;
			this.Options = options;
		}

		public PersistenceParameter Clone()
		{
			return new PersistenceParameter(this.Name, this.Value, this.Direction, this.Options != null ? this.Options.Clone() : null);
		}
	}

	public abstract class PersistenceParameterOptions
	{
		public abstract PersistenceParameterOptions Clone();
	}
}
