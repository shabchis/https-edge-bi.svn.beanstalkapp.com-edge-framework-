using System;

namespace Edge.Data.Objects
{
	public partial class TextCreativeDefinition : SingleCreativeDefinition
	{
		protected override Type CreativeType
		{
			get { return typeof(TextCreative); }
		}

		public new TextCreative Creative
		{
			get { return base.Creative as TextCreative; }
			//set { base.Creative = value; }
		}
	}
}
