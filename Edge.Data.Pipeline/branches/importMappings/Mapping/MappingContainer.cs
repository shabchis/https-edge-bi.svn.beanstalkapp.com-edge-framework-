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
		/// The parent map container.
		/// </summary>
		public MappingContainer Parent { get; internal set; }

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

		internal Dictionary<string, ReadCommand> InheritedReads = new Dictionary<string, ReadCommand>();

		internal void Inherit()
		{
			InheritedReads.Clear();

			// Get stuff from myself
			foreach (ReadCommand read in this.ReadCommands)
			{
				InheritedReads.Add(read.Name, read);
			}

			// Merge with inherited
			if (this.Parent != null)
			{
				// Merge reads
				foreach (var readPair in this.Parent.InheritedReads)
					if (!this.InheritedReads.ContainsKey(readPair.Key))
						this.InheritedReads.Add(readPair.Key, readPair.Value);
			}
		}

		public void Apply(object target)
		{
			if (target == null)
				throw new ArgumentNullException("target");

			if (!this.TargetType.IsAssignableFrom(target.GetType()))
				throw new ArgumentException(String.Format("This mapping can only be applied to objects of type {0}.", this.TargetType), "target");

			this.OnApply(target, new MappingContext());
		}

		protected virtual void OnApply(object target, MappingContext context)
		{
			foreach (MapCommand command in this.MapCommands)
				command.OnApply(target, context);
		}
	}

}
