using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Mapping
{
	public class MappingContainer
	{
		/// <summary>
		/// The target type to which this mapping is to be applied, derived from Parent.
		/// </summary>
		public Type TargetType { get; set; }

		/// <summary>
		/// The map commands performed in this item.
		/// </summary>
		public List<MapCommand> MapCommands { get; private set; }

		/// <summary>
		/// The reads commands performed in this item.
		/// </summary>
		public List<ReadCommand> ReadCommands { get; private set; }

		public MappingContainer()
		{
			this.MapCommands = new List<MapCommand>();
			this.ReadCommands = new List<ReadCommand>();
		}
	}

}
