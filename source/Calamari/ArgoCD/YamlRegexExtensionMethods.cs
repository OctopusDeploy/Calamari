using System;
using System.Text.RegularExpressions;

namespace Calamari.ArgoCD
{
    public static class YamlRegexExtensionMethods
    {

        static readonly Regex TrailingDocumentSeparatorRegex =
            new Regex(@"(?m)^\s*---\s*$", RegexOptions.Compiled);

        static readonly Regex DocumentSplitRegex =
            new Regex(@"(?<=^---\s*$)", RegexOptions.Multiline | RegexOptions.Compiled);

        public static string RemoveDocumentSeparators(this string yaml) =>
            TrailingDocumentSeparatorRegex.Replace(yaml, string.Empty);

        public static string[] SplitYamlDocuments(this string yaml) =>
            DocumentSplitRegex.Split(yaml);
    }
}
