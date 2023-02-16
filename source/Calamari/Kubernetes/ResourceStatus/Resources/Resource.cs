using Newtonsoft.Json.Linq;

namespace Calamari.ResourceStatus.Resources;

/// <summary>
/// Represents a kubernetes resource in a cluster, including its status
/// </summary>
public class Resource
{
    public JObject Data { get; }
    public string Uid => Field("$.metadata.uid");
    public string Kind => Field("$.kind");
    public string Name => Field("$.metadata.name");
    public string Namespace => Field("$.metadata.namespace");

    public virtual string ChildKind => "";

    public Resource(JObject json) => Data = json;

    public string Field(string jsonPath) => Data.SelectToken(jsonPath)?.Value<string>() ?? "";

    public virtual (ResourceStatus, string) CheckStatus() => (ResourceStatus.Successful, "Unsupported resource type is always treated as successful");

    // TODO remove this once it's not needed
    public virtual string StatusToDisplay => Data.SelectToken("$.status")?.ToString();
}