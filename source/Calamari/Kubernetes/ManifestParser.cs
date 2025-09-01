using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes
{
    public static class ManifestParser
    {
        public static List<ResourceIdentifier> GetResourcesFromManifest(string manifest,
                                                                        IKubernetesManifestNamespaceResolver namespaceResolver,
                                                                        IVariables variables,
                                                                        ILog log)
        {
            var resources = new List<ResourceIdentifier>();
            try
            {
                var yamlStream = new YamlStream();
                yamlStream.Load(new StringReader(manifest));

                foreach (var document in yamlStream.Documents)
                {
                    if (!(document.RootNode is YamlMappingNode rootNode))
                    {
                        log.Warn("Could not parse manifest, resources will not be added to object status");
                        continue;
                    }

                    var gvk = rootNode.ToResourceGroupVersionKind();
                    var metadataNode = rootNode.GetChildNode<YamlMappingNode>("metadata");
                    var name = metadataNode.GetChildNode<YamlScalarNode>("name").Value;
                    var @namespace = namespaceResolver.ResolveNamespace(rootNode, variables);

                    resources.Add(new ResourceIdentifier(gvk, name, @namespace));
                }
            }
            catch (YamlException yamlEx)
            {
                log.Warn("Could not parse manifest, resources will not be added to object status");
                log.Verbose($"YAML error: {yamlEx.Message}");
            }

            return resources;
        }
    }
}