using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes
{
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