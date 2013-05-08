using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class Measure
	{
		public int ID;
		public Account Account;
		public string Name;
		public MeasureDataType DataType;
		public string DisplayName;
		public string StringFormat;
		public MeasureOptions Options;
		public bool OptionsOverride;
		public bool IsInstance;
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
