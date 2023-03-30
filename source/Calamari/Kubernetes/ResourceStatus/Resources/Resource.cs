using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    /// <summary>
    /// Represents a kubernetes resource in a cluster, including its status
    /// </summary>
    public class Resource
    {
        [JsonIgnore] 
        protected JObject data;
        
        [JsonIgnore]
        public IEnumerable<string> OwnerUids { get; }
        
        [JsonIgnore]
        public string Uid { get; }
        [JsonIgnore]
        public string Kind { get; }
        [JsonIgnore]
        public string Name { get; }
        [JsonIgnore]
        public string Namespace { get; }
        [JsonIgnore]
        public virtual ResourceStatus ResourceStatus => ResourceStatus.Successful;
        [JsonIgnore]
        public virtual string ChildKind => "";
    
        [JsonIgnore]
        public IEnumerable<Resource> Children { get; internal set; }
    
        public Resource(JObject json)
        {
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