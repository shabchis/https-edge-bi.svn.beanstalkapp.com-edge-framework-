﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	[TableInfo(Name = "TextCreative")]
	public partial class TextCreative : SingleCreative
	{
		public TextCreativeType TextType;
		public string Text;
	}

	public enum TextCreativeType
	{
		Text = 1,
		Url = 2
	}

}
