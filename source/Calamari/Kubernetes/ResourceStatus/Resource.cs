using Newtonsoft.Json.Linq;

namespace Calamari.ResourceStatus;

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

    public Resource(string rawJson)
    {
        data = JObject.Parse(rawJson);
    }

    public Resource(JObject json)
    {
        data = json;
    }

    public T Field<T>(string jsonPath) => data.SelectToken(jsonPath).Value<T>();

    // TODO remove this once it's not needed
    public string StatusToDisplay => data.SelectToken("$.status").ToString();
}