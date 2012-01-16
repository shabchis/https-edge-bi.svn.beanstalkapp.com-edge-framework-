using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Pipeline.Mapping
{
	internal abstract class MappingContainer
	{
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
