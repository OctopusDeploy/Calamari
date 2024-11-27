using System;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities.Extensions;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ResourceGroupVersionKind : IEquatable<ResourceGroupVersionKind>
    {
        public ResourceGroupVersionKind(string group, string version, string kind)
        {
            Group = group;
            Version = version;
            Kind = kind;
        }

        public string Group { get; }
        public string Version { get; }
        public string Kind { get; }
        public bool Equals(ResourceGroupVersionKind other)
        {
            return other != null
                   && Group == other.Group
                   && Version == other.Version
                   && Kind == other.Kind;
        }
        
        public override bool Equals(object obj)
        {
            return obj is ResourceGroupVersionKind other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Group != null ? Group.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Version != null ? Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Kind != null ? Kind.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return !Group.IsNullOrEmpty() ? $"{Group}/{Version}/{Kind}" : $"{Version}/{Kind}";
        }
    }

    public static class ResourceGroupVersionKindExtensionMethods
    {
        public static ResourceGroupVersionKind ToResourceGroupVersionKind(this JObject jObject)
        {
            var kind = FieldOrDefault(jObject, "$.kind", "");
            var apiVersion = FieldOrDefault(jObject, "$.apiVersion", "");
            var (group, version) = ParseApiVersion(apiVersion);

            return new ResourceGroupVersionKind(group, version, kind);
        }
        
        public static ResourceGroupVersionKind ToResourceGroupVersionKind(this YamlMappingNode rootNode)
        {
            var kind = rootNode.GetChildNodeIfExists<YamlScalarNode>("kind")?.Value ?? "";
            var apiVersion = rootNode.GetChildNodeIfExists<YamlScalarNode>("apiVersion")?.Value ?? "";
            var (group, version) = ParseApiVersion(apiVersion);

            return new ResourceGroupVersionKind(group, version, kind);
        }

        public static (string Group, string Version) ParseApiVersion(string apiVersion)
        {
            if (apiVersion.IsNullOrEmpty())
            {
                return (null, null);
            }
            
            var apiVersionParts = apiVersion.Split('/');
            switch (apiVersionParts.Length)
            {
                case 1:
                    return ("", apiVersionParts[0]);
                case 2:
                    return (apiVersionParts[0], apiVersionParts[1]);
                default:
                    return (null, null);
            }
        } 
        
        static string FieldOrDefault(JObject jObject, string jsonPath, string defaultValue)
        {
            var result = jObject.SelectToken(jsonPath);
            if (result == null)
            {
                return defaultValue;
            }
            try
            {
                return result.Value<string>();
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}