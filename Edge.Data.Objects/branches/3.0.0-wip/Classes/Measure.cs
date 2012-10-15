using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class Measure
	{
		public int ID;
		public string Name;
		public string DisplayName;
		public Account Account;
		public Channel Channel;
		public Measure BaseMeasure;
		public string StringFormat;
		public MeasureDataType DataType; // if true, table manager adds another column called {name}_Converted
		public MeasureOptions Options;
	}

	public enum MeasureDataType
	{
		Number = 1,
		Currency = 2
	}

	[Flags]
	public enum MeasureOptions
	{
		None = 0x0,
		ChecksumRequired = 0x80,
		All = 0xff
	}

	
}
