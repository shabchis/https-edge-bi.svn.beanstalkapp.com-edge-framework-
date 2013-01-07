using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public abstract partial class CreativeReference : ChannelSpecificObject
	{
		private Creative _creative;

		public EdgeObject Parent;

		public Creative Creative
		{
			get { return _creative; }
			set
			{
				if (value != null && this.CreativeType.IsAssignableFrom(value.GetType()))
					throw new ArgumentException(String.Format("{0}.Creative must be of type {1}.", this.GetType().Name, this.CreativeType.Name), "value");
				_creative = value;
			}
		}

		protected abstract Type CreativeType
		{
			get;
		}
	}
}
