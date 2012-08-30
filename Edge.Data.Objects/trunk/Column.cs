using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;


namespace Edge.Data.Objects
{
	class Column
	{
		public string Name { get; set; }
		public SqlDbType DbType { get; set; }
		public int Length { get; set; }
	}
}
