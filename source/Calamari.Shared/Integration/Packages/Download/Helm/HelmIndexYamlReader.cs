using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.Versioning;
using Octopus.Versioning.Semver;
using YamlDotNet.RepresentationModel;

namespace Calamari.Integration.Packages.Download.Helm
{
    public static class HelmIndexYamlReader
    {
        public static IEnumerable<(string PackageId, IEnumerable<ChartData> Versions)> Read(YamlStream yaml)
        {
            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
                
            var apiVersion = ((YamlScalarNode)mapping.Children[new YamlScalarNode("apiVersion")]).Value;
            if (apiVersion != "v1")
            {
                throw new InvalidOperationException($"Octopus Deploy only supports the Helm repository api version 'v1'.\r\nThe version returned by this endpoint was '{apiVersion}'");
            }
                
            var entries = (YamlMappingNode)mapping.Children[new YamlScalarNode("entries")];
            foreach (var node in entries)
            {
                var packageId = (YamlScalarNode)node.Key;
                yield return (packageId.Value, Foo((YamlSequenceNode)node.Value));
            }
        }
        
        static IEnumerable<ChartData> Foo(YamlSequenceNode packageVersions)
        {
            foreach (var yamlNode in packageVersions)
            {
                var node = ChartData.FromNode((YamlMappingNode)yamlNode);
                if (node != null)
                {
                    yield return node;
                }
            }
        }

        public class ChartData
        {
            readonly YamlMappingNode yamlNode;

            ChartData(YamlMappingNode yamlNode, IVersion version)
            {
                this.yamlNode = yamlNode;
                Version = version;
            }

            public static ChartData FromNode(YamlMappingNode yamlNode)
            {
                if (!SemVerFactory.TryCreateVersion(((YamlScalarNode) yamlNode.Children[new YamlScalarNode("version")]).Value, out var version))
                {
                    return null;
                }

                if (yamlNode.Children.TryGetValue(new YamlScalarNode("deprecated"), out var deprecatedNode) &&
                    bool.TryParse(((YamlScalarNode) deprecatedNode).Value, out var isDeprecated) &&
                    isDeprecated)
                {
                    return null;
                }
                
                return new ChartData(yamlNode, version);
            }
            
            public IVersion Version { get; }

            public string Description => yamlNode.Children.TryGetValue(new YamlScalarNode("description"), out var descriptionNode) ?
                ((YamlScalarNode) descriptionNode).Value :
                null;

            public string Name => ((YamlScalarNode) yamlNode.Children[new YamlScalarNode("name")]).Value;

            public DateTimeOffset? Published
            {
                get
                {
                    if (yamlNode.Children.TryGetValue(new YamlScalarNode("created"), out var createdNode) &&
                        DateTimeOffset.TryParse(((YamlScalarNode) createdNode).Value, out var created))
                    {
                        return created;
                    }

                    return null;
                }
            }

            public IEnumerable<string> Urls
            {
                get
                {
                    return ((YamlSequenceNode) yamlNode.Children[new YamlScalarNode("urls")]).Children.Select(t => ((YamlScalarNode) t).Value);
                }
            
            }
        }
    }
}