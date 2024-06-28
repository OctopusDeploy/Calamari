//#if NETFRAMEWORK
using System;
using System.Text.RegularExpressions;

namespace Calamari.Tests.Helpers
{
    public static class StringExtensions
    {
        static readonly Regex Regex = new Regex("\r\n|\n\r|\n|\r", RegexOptions.Compiled);
        
        public static string ReplaceLineEndings(this string value) => Regex.Replace(value, Environment.NewLine);
    }
}
//#endif