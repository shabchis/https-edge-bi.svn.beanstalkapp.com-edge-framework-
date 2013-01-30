namespace Edge.Data.Pipeline.Metrics.Misc
{
	#region Extensions
	public static class StringExtenstions
	{
		public static string RemoveInvalidCharacters(this string expression)
		{
			return expression.Replace("'", "");
		}
	}
	
	#endregion
}
