using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Edge.Data.Objects
{
	public partial class TextCreativeMatch : SingleCreativeMatch
	{
		protected override Type CreativeType
		{
			get { return typeof(TextCreative); }
		}

		public new TextCreative Creative
		{
			get { return (TextCreative)base.Creative; }
			set { base.Creative = value; }
		}
	}
}
