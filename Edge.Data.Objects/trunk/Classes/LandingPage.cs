using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[TableInfo(Name = "LandingPage")]
	public partial class LandingPage : EdgeObject
	{
		public LandingPageType LandingPageType;
	}

	public enum LandingPageType
	{
		Static,
		Dynamic
	}

}
