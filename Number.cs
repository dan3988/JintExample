namespace JintTest
{
	internal static class Number
	{
		/// <summary>
		/// Used to calculate dates in excel format
		/// </summary>
		/// <remarks>
		/// Excel stores the date as the amount of days since 00-01-1900. Excel incorrectly counts 1900 as a leap year, so we have to go a day back to compensate for this. Any date value after 28-02-1900 will be the same as Excel.
		/// </remarks>
		public static readonly DateTime ZeroDate = new(1899, 12, 30);
	}
}
