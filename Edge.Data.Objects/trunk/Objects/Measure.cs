using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public class Measure
	{
		public int ID;
		public string Name;
		public MeasureDataType DataType; // if true, table manager adds another column called {name}_Converted
		public MeasureOptions Options;
	}

	public enum MeasureDataType
	{
		Number,
		Currency
	}

	[Flags]
	public enum MeasureOptions
	{
		None = 0x0,
		ChecksumRequired = 0x80,
		All = 0xff
	}

	
}
