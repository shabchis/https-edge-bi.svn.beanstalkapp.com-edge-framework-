using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects.Objects
{
	public class Measure
	{
		public int ID;
		public string Name;
		public bool IsCurrency; // if true, table manager adds another column called {name}_Converted
	}
}
