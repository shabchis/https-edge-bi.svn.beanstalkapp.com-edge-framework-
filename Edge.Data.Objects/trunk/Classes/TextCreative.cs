using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class TextCreative : SingleCreative
	{
		public TextCreativeType TextType;
		public string Text;

		public override string ToString()
		{
			return String.Format("Type:{0}_Text:{1}", TextType, Text);
		}
	}

	public enum TextCreativeType
	{
		Text = 1,
		Url = 2
	}

}
