using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Eggplant.Entities.Model;

namespace Edge.Data.Objects
{
	public partial class EdgeField
	{
		public static class Identities
		{
			public static IdentityDefinition Default = new IdentityDefinition(EdgeField.Properties.FieldID);
		}
	}
}
