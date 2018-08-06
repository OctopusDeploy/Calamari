using System;
using System.Linq;
using System.Text;

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

        public static byte[] EncodeInUtf8Bom(this string source)
        {
            return Encoding.UTF8.GetPreamble().Concat(source.EncodeInUtf8NoBom()).ToArray();
        }

        public static byte[] EncodeInUtf8NoBom(this string source)
        {
            return Encoding.UTF8.GetBytes(source);
        }
    }
}