using Newtonsoft.Json.Linq;

namespace Calamari.ResourceStatus.Resources;

/// <summary>
/// Represents a kubernetes resource in a cluster, including its status
/// </summary>
public class Resource
{
    public JObject Data { get; }
    public string Uid => Field<string>("$.metadata.uid");
    public string Kind => Field<string>("$.kind");
    public string Name => Field<string>("$.metadata.name");
    public string Namespace => Field<string>("$.metadata.namespace");

    public virtual string ChildKind => "";

    public Resource(JObject json) => Data = json;

    public T Field<T>(string jsonPath) => Data.SelectToken(jsonPath).Value<T>();

    public virtual (ResourceStatus, string) CheckStatus() => (ResourceStatus.Successful, "Unsupported resource type is always treated as successful");

    // TODO remove this once it's not needed
    public string StatusToDisplay => Data.SelectToken("$.status")?.ToString();
}