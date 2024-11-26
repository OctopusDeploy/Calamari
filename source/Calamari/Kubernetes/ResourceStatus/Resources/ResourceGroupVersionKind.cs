using System;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ResourceGroupVersionKind
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
    }

    public static class ResourceGroupVersionKindExtensionMethods
    {
        public static ResourceGroupVersionKind ToResourceGroupVersionKind(this JObject data)
        {
            var kind = FieldOrDefault(data, "$.kind", "");
            
            var apiVersion = FieldOrDefault(data, "$.apiVersion", "");
            var apiVersionParts = apiVersion.Split('/');

            switch (apiVersionParts.Length)
            {
                case 1:
                    return new ResourceGroupVersionKind("", apiVersionParts[0], kind);
                case 2:
                    return new ResourceGroupVersionKind(apiVersionParts[0], apiVersionParts[1], kind);
                default:
                    return new ResourceGroupVersionKind("", "", kind);
            }
        }
        
        static string FieldOrDefault(JObject data, string jsonPath, string defaultValue)
        {
            var result = data.SelectToken(jsonPath);
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