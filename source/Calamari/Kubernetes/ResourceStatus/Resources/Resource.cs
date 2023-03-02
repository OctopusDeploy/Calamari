using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    /// <summary>
    /// Represents a kubernetes resource in a cluster, including its status
    /// </summary>
    public class Resource
    {
        [JsonIgnore] protected JObject data;
        
        [JsonIgnore]
        public IEnumerable<string> OwnerUids { get; }
    
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? Removed { get; set; }
        
        public string ActionId { get; }
        public string Cluster { get; }
        public string Uid { get; }
        public string Kind { get; }
        public string Name { get; }
        public string Namespace { get; }
        
        // TODO what's a good default?
        [JsonConverter(typeof(StringEnumConverter))]
        public virtual ResourceStatus Status => ResourceStatus.Successful;
    
        [JsonIgnore]
        public virtual string ChildKind => "";
    
        [JsonIgnore]
        public IEnumerable<Resource> Children { get; set; }
    
        public Resource(JObject json, string cluster, string actionId)
        {
            Cluster = cluster;
            ActionId = actionId;
            data = json;
            OwnerUids = data.SelectTokens("$.metadata.ownerReferences[*].uid").Values<string>();
            Uid = Field("$.metadata.uid");
            Kind = Field("$.kind");
            Name = Field("$.metadata.name");
            Namespace = Field("$.metadata.namespace");
        }
    
        public virtual bool HasUpdate(Resource lastStatus) => false;
    
        protected string Field(string jsonPath) => FieldOrDefault(jsonPath, "");
        
        protected T FieldOrDefault<T>(string jsonPath, T defaultValue)
        {
            var result = data.SelectToken(jsonPath);
            return result == null ? defaultValue : result.Value<T>();
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