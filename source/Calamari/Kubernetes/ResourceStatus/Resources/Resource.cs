using Newtonsoft.Json.Linq;

namespace Calamari.ResourceStatus.Resources;

/// <summary>
/// Represents a kubernetes resource in a cluster, including its status
/// </summary>
public class Resource
{
    private readonly JObject data;

    public string Uid => Field<string>("$.metadata.uid");
    public string Kind => Field<string>("$.kind");
    public string Name => Field<string>("$.metadata.name");
    public string Namespace => Field<string>("$.metadata.namespace");
    public string RawJson => data.ToString();
    public virtual string ChildKind => "";

    public Resource(JObject json) => data = json;

    public T Field<T>(string jsonPath) => data.SelectToken(jsonPath).Value<T>();

    public virtual (ResourceStatus, string) CheckStatus() => (ResourceStatus.Successful, "Unsupported resource type is always treated as successful");

    // TODO remove this once it's not needed
    public string StatusToDisplay => data.SelectToken("$.status")?.ToString();
}