using System;

namespace Calamari.Integration.ConfigurationTransforms
{
    public class XmlConfigTransformDefinition
    {
        public XmlConfigTransformDefinition(string definition)
        {
            if (definition.Contains("=>"))
            {
                Advanced = true;
                var separators = new[] {"=>"};
                var parts = definition.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                TransformPattern = parts[0].Trim();
                SourcePattern = parts[1].Trim();

                if (TransformPattern.StartsWith("*."))
                {
                    Wildcard = true;
                    TransformPattern = TrimWildcardPattern(TransformPattern);
                }

                if (SourcePattern.StartsWith("*."))
                {
                    Wildcard = true;
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
            return (pattern.LastIndexOf('.') > 2)
                ? pattern.Remove(0, 2)
                : pattern.Remove(0, 1);
        }

        public string TransformPattern { get; private set; }
        public string SourcePattern { get; private set; }
        public bool Wildcard { get; private set; }
        public bool Advanced { get; private set; }
    }
}