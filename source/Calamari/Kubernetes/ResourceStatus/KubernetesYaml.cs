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
        private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

        /// <summary>
        /// Gets resource identifiers which are defined in a YAML file.
        /// A YAML file can define multiple resources, separated by '---'.
        /// </summary>
        public static IEnumerable<ResourceIdentifier> GetDefinedResources(string manifests)
        {
            var input = new StringReader(manifests);

            var parser = new Parser(input);
            parser.Consume<StreamStart>();

            var result = new List<ResourceIdentifier>();
            while (!parser.Accept<StreamEnd>(out _))
            {
                result.Add(GetDefinedResource(parser));
            }

            return result;
        }

        private static ResourceIdentifier GetDefinedResource(IParser parser)
        {
            var yamlObject = Deserializer.Deserialize<dynamic>(parser);
            yamlObject["metadata"].TryGetValue("namespace", out object @namespace);
            return new ResourceIdentifier(
                yamlObject["kind"],
                yamlObject["metadata"]["name"],
                @namespace == null ? "default" : (string) @namespace);
        }
    }
}