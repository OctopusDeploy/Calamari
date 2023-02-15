using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Constructs an IEnumerable of Resources from a kubernetes API response JSON of the "list" action.
    /// The response JSON contains a top-level "items" field which contains the resources
    /// </summary>
    public static IEnumerable<Resource> FromListResponse(string rawJson)
    {
        var listResponse = JObject.Parse(rawJson);
        return listResponse.SelectTokens("$.items[*]").Select(item => new Resource((JObject) item));
    }

    public T Field<T>(string jsonPath) => data.SelectToken(jsonPath).Value<T>();

    // TODO remove this once it's not needed
    public string StatusToDisplay => data.SelectToken("$.status").ToString();

    public string ChildKind => Kind switch
    {
        "Deployment" => "ReplicaSet",
        "ReplicaSet" => "Pod",
        _ => ""
    };
}