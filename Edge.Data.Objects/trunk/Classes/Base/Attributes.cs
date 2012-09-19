using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	sealed class TableInfoAttribute : Attribute
	{
		public string Name
		{
			get;
			set;
		}
	}
}
