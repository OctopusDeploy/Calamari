#nullable enable
using System;
using System.IO;
using YamlDotNet.RepresentationModel;

namespace Calamari.ArgoCD
{
    public static class KustomizationValidator
    {
        public static bool IsKustomizationResource(string content)
        {
            try
            {
                var yamlStream = new YamlStream();
                yamlStream.Load(new StringReader(content));

                if (yamlStream.Documents.Count == 0)
                {
                    return false;
                }

                var rootNode = yamlStream.Documents[0].RootNode;
                if (rootNode is not YamlMappingNode mappingNode)
                {
                    return false;
                }

                if (!mappingNode.Children.TryGetValue(new YamlScalarNode("apiVersion"), out var apiVersionNode) ||
                    apiVersionNode is not YamlScalarNode apiVersionScalar)
                    return false;

                if (!mappingNode.Children.TryGetValue(new YamlScalarNode("kind"), out var kindNode) ||
                    kindNode is not YamlScalarNode kindScalar)
                    return false;

                var apiVersion = apiVersionScalar.Value ?? "";
                var kind = kindScalar.Value ?? "";

                return apiVersion.StartsWith("kustomize.config.k8s.io", StringComparison.OrdinalIgnoreCase) &&
                       (kind.Equals("Kustomization", StringComparison.OrdinalIgnoreCase) ||
                        kind.Equals("Component", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }
}