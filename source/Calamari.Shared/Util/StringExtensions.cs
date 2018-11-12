using System;
using System.IO;
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

        public static string EnsureSuffix(this string source, string suffix)
        {
            return !source.EndsWith(suffix) ? $"{source}{suffix}" : source;
        }

        public static string EnsurePreix(this string source, string prefix)
        {
            return !source.StartsWith(prefix) ? $"{prefix}{source}" : source;
        }

        public static string AsRelativePathFrom(this string source, string baseDirectory)
        {
            var uri = new Uri(source);
            if (!baseDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                baseDirectory += Path.DirectorySeparatorChar.ToString();
            }
            var baseUri = new Uri(baseDirectory);
            return baseUri.MakeRelativeUri(uri).ToString();
        }
    }
}