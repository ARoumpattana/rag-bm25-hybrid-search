using System.Text.RegularExpressions;

namespace KeyMatch.Utils
{
    public static class CleanUpTextUtils
    {
        public static string CleanUpStandardTextSearch(string input)
        {
            string cleanInput = Regex.Replace(input, @"[^\w\s]", " ").Trim().ToLower();
            return cleanInput;
        }
    }
}
