using System;

namespace Calamari.Util
{
    public static class StringExtensions
    {
        public static bool ContainsIgnoreCase(this string originalString, string value)
        {
            return originalString.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) != -1;
        }

        public static string EscapeSingleQuotedString(this string str) =>
            str.Replace("'", "''");
    }
}