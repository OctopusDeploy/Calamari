using System;
using Newtonsoft.Json.Linq;
using Octopus.CoreUtilities.Extensions;
using YamlDotNet.RepresentationModel;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
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
                    throw new InvalidOperationException($"Invalid API Version: {apiVersion}, must conform to <group>/<version> naming convention.");
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