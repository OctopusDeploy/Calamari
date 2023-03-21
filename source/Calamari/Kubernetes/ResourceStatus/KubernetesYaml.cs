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
        public static IEnumerable<ResourceIdentifier> GetDefinedResources(string manifests, string defaultNamespace)
        {
            var input = new StringReader(manifests);

            var parser = new Parser(input);
            parser.Consume<StreamStart>();
            
            while (!parser.Accept<StreamEnd>(out _))
            {
                var definedResource = GetDefinedResource(parser, defaultNamespace);
                if (definedResource != null)
                {
                    yield return definedResource;
                }
            }
        }

        private static ResourceIdentifier GetDefinedResource(IParser parser, string defaultNamespace)
        {
            try
            {
                var yamlObject = Deserializer.Deserialize<dynamic>(parser);
                yamlObject["metadata"].TryGetValue("namespace", out object @namespace);
                return new ResourceIdentifier(
                    yamlObject["kind"],
                    yamlObject["metadata"]["name"],
                    @namespace == null ? defaultNamespace : (string) @namespace);
            }
            catch
            {
                return null;
            }
        }
    }
}