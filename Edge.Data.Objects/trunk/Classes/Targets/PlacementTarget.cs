using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[TableInfo(Name = "TargetPlacement")]
	public partial class PlacementTarget : Target
	{
		public string Value;
		public PlacementType PlacementType;
	}

	public enum PlacementType
	{
		Unidentified = 0,
		Automatic = 4,
		Managed = 5
	}

}
