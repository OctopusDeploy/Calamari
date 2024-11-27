using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Kubernetes.ResourceStatus.Resources;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
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
        public static IEnumerable<ResourceIdentifier> GetDefinedResources(IEnumerable<string> manifests, string defaultNamespace)
        {
            foreach (var manifest in manifests)
            {
                var input = new StringReader(manifest);

                var parser = new Parser(input);
                parser.Consume<StreamStart>();

                while (!parser.Accept<StreamEnd>(out _))
                {
                    var definedResource = GetDefinedResource(parser, defaultNamespace);
                    if (!definedResource.HasValue)
                        break;

                    yield return definedResource.Value;
                }
            }
        }

        static ResourceIdentifier? GetDefinedResource(IParser parser, string defaultNamespace)
        {
            try
            {
                var yamlObject = Deserializer.Deserialize<dynamic>(parser);
                yamlObject["metadata"].TryGetValue("namespace", out object @namespace);
                yamlObject.TryGetValue("apiVersion", out object apiVersion);
                var groupAndVersion = ResourceGroupVersionKindExtensionMethods.ParseApiVersion(apiVersion?.ToString());
                var gvk = new ResourceGroupVersionKind(groupAndVersion.Item1, groupAndVersion.Item2, yamlObject["kind"]);
                return new ResourceIdentifier(gvk,
                                              yamlObject["metadata"]["name"],
                                              @namespace == null ? defaultNamespace : (string)@namespace);
            }
            catch
            {
                return null;
            }
        }
    }
}