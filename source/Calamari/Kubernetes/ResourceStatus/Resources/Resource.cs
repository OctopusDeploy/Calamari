using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources;

/// <summary>
/// Represents a kubernetes resource in a cluster, including its status
/// </summary>
public class Resource
{
    [JsonIgnore]
    public JObject Data { get; }
    public string Uid { get; }
    public string Kind { get; }
    public string Name { get; }
    public string Namespace { get; }
    
    // TODO what's a good default?
    [JsonConverter(typeof(StringEnumConverter))]
    public virtual ResourceStatus Status => ResourceStatus.Successful;

    [JsonIgnore]
    public virtual string ChildKind => "";

    public Resource(JObject json)
    {
        Data = json;
        Uid = Field("$.metadata.uid");
        Kind = Field("$.kind");
        Name = Field("$.metadata.name");
        Namespace = Field("$.metadata.namespace");
    }

    public string Field(string jsonPath) => FieldOrDefault(jsonPath, "");
    
    public T FieldOrDefault<T>(string jsonPath, T defaultValue)
    {
        var result = Data.SelectToken(jsonPath);
        return result == null ? defaultValue : result.Value<T>();
    }

    // What is a good default?
    public virtual bool HasUpdate(Resource lastStatus) => true;
    
    // TODO remove this once it's not needed
    [JsonIgnore]
    public virtual string StatusToDisplay => Data.SelectToken("$.status")?.ToString();
    
    protected static T CastOrThrow<T>(Resource resource) where T: Resource
    {
        if (resource is not T subType)
        {
            throw new Exception($"Cannot cast resource to subtype {nameof(T)}");
        }

        return subType;
    }
}