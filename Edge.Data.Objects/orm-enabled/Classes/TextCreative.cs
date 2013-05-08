namespace Edge.Data.Objects
{
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
