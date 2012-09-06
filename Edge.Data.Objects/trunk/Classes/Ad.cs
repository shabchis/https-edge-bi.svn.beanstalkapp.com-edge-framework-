using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class Ad : ChannelSpecificObject
	{
		public string DestinationUrl;
		public Creative Creative;
		public override bool HasChilds()
		{
			if (Creative!=null)
				return true;
			else
				return false;
		}

		public override IEnumerable<EdgeObject> GetChildObjects()
		{
			yield return this.Creative;
		}
	}

}
