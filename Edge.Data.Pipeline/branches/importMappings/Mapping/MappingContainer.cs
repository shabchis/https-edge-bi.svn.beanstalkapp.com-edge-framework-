using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Mapping
{
	public class MappingContainer
	{
		/// <summary>
		/// The configuration that this container belongs to.
		/// </summary>
		public MappingConfiguration Root { get; internal set; }

		/// <summary>
		/// The target type to which this mapping is to be applied.
		/// </summary>
		public Type TargetType { get; internal set; }

		/// <summary>
		/// The map commands performed in this item.
		/// </summary>
		public List<MapCommand> MapCommands { get; private set; }

		/// <summary>
		/// The reads commands performed in this item.
		/// </summary>
		public List<ReadCommand> ReadCommands { get; private set; }

		internal MappingContainer()
		{
			this.MapCommands = new List<MapCommand>();
			this.ReadCommands = new List<ReadCommand>();
		}
	}

}
