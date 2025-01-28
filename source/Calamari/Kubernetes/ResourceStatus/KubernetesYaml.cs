using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace Calamari.Kubernetes.ResourceStatus
{
    public static class KubernetesYaml
    {
        static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

        /// <summary>
        /// Gets resource identifiers which are defined in a YAML file.
        /// A YAML file can define multiple resources, separated by '---'.
        /// </summary>
        public static IEnumerable<ResourceIdentifier> GetDefinedResources(IEnumerable<string> manifests,
                                                                          IKubernetesManifestNamespaceResolver namespaceResolver,
                                                                          IVariables variables,
                                                                          ILog log)
        {
            foreach (var manifest in manifests)
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

                    ResourceGroupVersionKind gvk;
                    string name;
                    string @namespace;
                    try
                    {
                        gvk = rootNode.ToResourceGroupVersionKind();
                        var metadataNode = rootNode.GetChildNode<YamlMappingNode>("metadata");
                        name = metadataNode.GetChildNode<YamlScalarNode>("name").Value;
                        @namespace = namespaceResolver.ResolveNamespace(rootNode, variables);
                    }
                    catch (YamlException)
                    {
                        log.Warn("Could not parse manifest, resources will not be added to object status");
                        continue;
                    }
                    
                    yield return new ResourceIdentifier(gvk, name, @namespace);
                }
            }
        }
    }
}