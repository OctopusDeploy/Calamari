using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    /// <summary>
    /// Represents a kubernetes resource in a cluster, including its status
    /// </summary>
    public class Resource : IResourceIdentity
    {
        [JsonIgnore] public IEnumerable<string> OwnerUids { get; }

        [JsonIgnore] public string Uid { get; protected set; }

        [JsonIgnore] public ResourceGroupVersionKind GroupVersionKind { get; protected set; }
        [JsonIgnore] public string Name { get; }
        [JsonIgnore] public string Namespace { get; }

        [JsonIgnore] public virtual ResourceStatus ResourceStatus { get; set; } = ResourceStatus.Successful;

        [JsonIgnore]
        public virtual ResourceGroupVersionKind ChildGroupVersionKind => default;

        [JsonIgnore]
        public IEnumerable<Resource> Children { get; internal set; }

        internal Resource() { }

        // json stays a parameter - retaining it pinned the whole parsed document via JToken.Parent
        public Resource(JObject json, Options options)
        {
            // force enumeration to prevent memory growth
            OwnerUids = json.SelectTokens("$.metadata.ownerReferences[*].uid").Values<string>().ToList();
            Uid = Field(json, "$.metadata.uid");
            GroupVersionKind = json.ToResourceGroupVersionKind();
            Name = Field(json, "$.metadata.name");
            //we explicitly want null if there is no namespace
            Namespace = FieldOrDefault<string>(json, "$.metadata.namespace", null);
        }

        public virtual bool HasUpdate(Resource lastStatus) => false;

        public virtual void UpdateChildren(IEnumerable<Resource> children) => Children = children;

        protected static string Field(JObject data, string jsonPath) => FieldOrDefault(data, jsonPath, "");

        protected static T FieldOrDefault<T>(JObject data, string jsonPath, T defaultValue)
        {
            var result = data.SelectToken(jsonPath);
            if (result == null)
            {
                return defaultValue;
            }
            try
            {
                return result.Value<T>();
            }
            catch
            {
                return defaultValue;
            }
        }

        protected static T CastOrThrow<T>(Resource resource) where T: Resource
        {
            if (resource is T subType)
            {
                return subType;
            }
            throw new Exception($"Cannot cast resource to subtype {nameof(T)}");
        }
    }
}
