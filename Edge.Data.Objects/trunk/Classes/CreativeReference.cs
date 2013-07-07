using System;
using System.Collections.Generic;
using System.Linq;

namespace Edge.Data.Objects
{
	public abstract partial class CreativeReference : ChannelSpecificObject
	{
		private Creative _creative;

		//public EdgeObject Parent;
		//public string DestinationUrl;
		public Destination Destination;
		public Creative Creative
		{
			get { return _creative; }
			set
			{
				if (value != null && !CreativeType.IsInstanceOfType(value))
					throw new ArgumentException(String.Format("{0}.Creative must be of type {1}.", GetType().Name, CreativeType.Name), "value");
				_creative = value;
			}
		}

		protected abstract Type CreativeType
		{
			get;
		}

		public override IEnumerable<ObjectDimension> GetObjectDimensions()
		{
			foreach (var dimension in base.GetObjectDimensions())
			{
				yield return dimension;
			}

			if (Destination != null) yield return new ObjectDimension
			{
				Field = EdgeType["Destination"],
				Value = Destination
			};

			if (Creative != null) yield return new ObjectDimension
			{
				Field = this.EdgeType[String.Format("{0}_Creative", this.EdgeType.Name)],
				Value = Creative
			};
		}
	}
}
