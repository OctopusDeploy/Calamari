using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    /// <summary>
    /// Represents a kubernetes resource in a cluster, including its status
    /// </summary>
    public class Resource : IResourceIdentity
    {
        [JsonIgnore] protected JObject data;

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

        public Resource(JObject json, Options options)
        {
            data = json;
            OwnerUids = data.SelectTokens("$.metadata.ownerReferences[*].uid").Values<string>();
            Uid = Field("$.metadata.uid");
            GroupVersionKind  = json.ToResourceGroupVersionKind();
            Name = Field("$.metadata.name");
            //we explicitly want null if there is no namespace
            Namespace = FieldOrDefault<string>("$.metadata.namespace", null);
        }

        public virtual bool HasUpdate(Resource lastStatus) => false;

        public virtual void UpdateChildren(IEnumerable<Resource> children) => Children = children;

        protected string Field(string jsonPath) => FieldOrDefault(jsonPath, "");

        protected T FieldOrDefault<T>(string jsonPath, T defaultValue)
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