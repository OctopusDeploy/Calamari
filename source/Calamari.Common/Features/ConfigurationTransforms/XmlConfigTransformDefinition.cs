using System;
using System.IO;

namespace Calamari.Common.Features.ConfigurationTransforms
{
    public class XmlConfigTransformDefinition
    {
        readonly string definition;

        public XmlConfigTransformDefinition(string definition)
        {
            this.definition = definition;
            if (definition.Contains("=>"))
            {
                Advanced = true;
                var separators = new[] {"=>"};
                var parts = definition.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                TransformPattern = parts[0].Trim();
                SourcePattern = parts[1].Trim();

                if (Path.GetFileName(TransformPattern).StartsWith("*."))
                {
                    IsTransformWildcard = true;
                    TransformPattern = TrimWildcardPattern(TransformPattern);
                }

                if (Path.GetFileName(SourcePattern).StartsWith("*."))
                {
                    IsSourceWildcard = true;
                    SourcePattern = TrimWildcardPattern(SourcePattern);
                }
            }
            else
            {
                TransformPattern = definition;
            }
        }

        static string TrimWildcardPattern(string pattern)
        {
            var wildcardIndex = pattern.IndexOf('*');
            return (pattern.LastIndexOf('.') > wildcardIndex + 1)
                ? pattern.Remove(wildcardIndex, 2)
                : pattern.Remove(wildcardIndex, 1);
        }

        public string TransformPattern { get; private set; }
        public string? SourcePattern { get; private set; }
        public bool IsTransformWildcard { get; private set; }
        public bool IsSourceWildcard { get; private set; }
        public bool Advanced { get; private set; }

        public override string ToString()
        {
            return definition;
        }
    }
}