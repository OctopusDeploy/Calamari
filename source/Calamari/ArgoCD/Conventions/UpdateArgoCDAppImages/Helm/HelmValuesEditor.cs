#if NET
using System;
using Octostache;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Helm
{
    public class HelmValuesEditor
    {
        /// <summary>
        /// Converts YAML into a Variable Dictionary that can be used to resolve the value of
        /// a dot-notation path and used with HelmTemplate/Octostache syntax.
        /// </summary>
        public static VariableDictionary GenerateVariableDictionary(HelmYamlParser parsedYaml)
        {
            var allValuesPaths = parsedYaml.CreateDotPathsForNodes();

            var variableDictionary = new VariableDictionary();
            foreach (var path in allValuesPaths)
            {
                var value = parsedYaml.GetValueAtPath(path);
                variableDictionary.Set(path, value);
            }

            return variableDictionary;
        }

        /// <summary>
        /// Updates the value of yaml a node and returns it as a strung (preserving formatting).
        /// </summary>
        public static string UpdateNodeValue(string yamlContent, string path, string newValue)
        {
            // Recreates HelmYamlParser rather than pass in because we maintain state as a string rather than yaml to preserve formatting.
            var yamlProcessor = new HelmYamlParser(yamlContent);
            return yamlProcessor.UpdateContentForPath(path, newValue);
        }
    }
}

#endif
