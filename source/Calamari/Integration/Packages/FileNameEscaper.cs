using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Calamari.Integration.Packages
{
    public static class FileNameEscaper
    {
        private static readonly HashSet<char> InvalidCharacterSet = new HashSet<char> {
            '%', '<', '>', ':', '"', '/', '\\', '|', '?'
        };

        public static char[] EscapedCharacters => InvalidCharacterSet.ToArray();

        /// <summary>
        /// Encodes invalid Windows filename characters < > : " / \ | ? *
        /// https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247(v=vs.85).aspx
        /// </summary>
        /// <returns></returns>
        public static string Escape(string input)
        {
            var sb = new StringBuilder();
            foreach (var c in input)
            {
                if (!InvalidCharacterSet.Contains(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append(Uri.EscapeDataString(c.ToString()));
                }
            }

            return sb.ToString();
        }

        public static string Unescape(string input)
        {
            return Uri.UnescapeDataString(input);
        }
    }
}
