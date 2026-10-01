using System;
using System.IO;
using Calamari.Common.Plumbing.Logging;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes
{
    public static class YamlStreamLoader
    {
        public static YamlStream? TryLoad(string yamlContent, ILog log, string context)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(yamlContent))
                {
                    log.Warn($"{context} file content is empty or whitespace only.");
                    return null;
                }

                using var reader = new StringReader(yamlContent);
                var stream = new YamlStream();
                stream.Load(reader);

                if (stream.Documents.Count == 0)
                {
                    log.Warn($"{context} file contains no YAML documents.");
                    return null;
                }

                return stream;
            }
            catch (YamlException ex)
            {
                log.WarnFormat("Invalid YAML in {0}: {1}", context.ToLower(), ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                log.WarnFormat("Error loading YAML documents from {0}: {1}", context.ToLower(), ex.Message);
                return null;
            }
        }
    }

    public static class YamlDotNetExtensionMethods
    {
        public static TNode GetChildNode<TNode>(this YamlMappingNode node, string key) where TNode: class
        {
            return GetChildNodeIfExists<TNode>(node, key) 
                   ?? throw new YamlException($"Could not parse manifest, the '{key}' property is missing");
        }

        public static TNode GetChildNodeIfExists<TNode>(this YamlMappingNode node, string key) where TNode: class
        {
            if (!node.Children.TryGetValue(key, out var childNode))
            {
                return null;
            }

            if (!(childNode is TNode typedChildNode))
            {
                throw new YamlException($"Could not parse manifest, the '{key}' property is the wrong type");
            }

            return typedChildNode;
        }
    }
}