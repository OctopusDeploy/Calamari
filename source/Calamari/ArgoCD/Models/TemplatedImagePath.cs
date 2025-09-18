#if NET
using System;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.ArgoCD.Mapping;
using Octostache;

namespace Calamari.ArgoCD.Models
{
    public class TemplatedImagePath
    {
        public TemplatedImagePath(string tagPath, ContainerImageReference imageReference, bool tagIsTemplateToken)
        {
            TagPath = tagPath;
            ImageReference = imageReference;
            TagIsTemplateToken = tagIsTemplateToken;
        }

        public string TagPath { get; }

        public ContainerImageReference ImageReference { get; }

        public bool TagIsTemplateToken { get; }

        public static TemplatedImagePath Parse(string template, VariableDictionary variables, string defaultRegistry)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                throw new ArgumentException("Template cannot be null or whitespace.", nameof(template));
            }
            if (variables.GetNames().Count <= 0)
            {
                throw new ArgumentException("Variables cannot be empty.", nameof(variables));
            }
            if (string.IsNullOrWhiteSpace(defaultRegistry))
            {
                throw new ArgumentException("Default registry cannot be null or whitespace.", nameof(defaultRegistry));
            }

            string tagPath;
            var tagIsTemplateToken = false;

            var octostacheTemplate = GoTemplatingToOctostacheConverter.ConvertToOctostache(template);

            var tagComponents = octostacheTemplate.Split(':');
            if (tagComponents.Length == 2)
            {
                tagPath = tagComponents[1].Trim();
                tagIsTemplateToken = true;
            }
            else
            {
                var lastToken = octostacheTemplate.Substring(octostacheTemplate.LastIndexOf("#{", StringComparison.Ordinal));
                tagPath = lastToken.Trim();
            }

            var imageReferenceValue = variables.Evaluate(octostacheTemplate);
            try
            {
                var rawTagPath = tagPath.Replace("#{", "").Replace("}", "").Trim();
                var imageReference = ContainerImageReference.FromReferenceString(imageReferenceValue, defaultRegistry);
                return new TemplatedImagePath(rawTagPath, imageReference, tagIsTemplateToken);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Failed to parse image reference from template: {template}", e);
            }
        }
    }
}
#endif
