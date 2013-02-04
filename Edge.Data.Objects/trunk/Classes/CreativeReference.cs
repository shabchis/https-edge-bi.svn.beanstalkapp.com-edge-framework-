using System;

namespace Edge.Data.Objects
{
	public abstract partial class CreativeReference : ChannelSpecificObject
	{
		private Creative _creative;

		public EdgeObject Parent;

		public string DestinationUrl;

		public virtual Creative Creative
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
	}
}
