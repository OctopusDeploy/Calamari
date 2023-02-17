using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources;

public static class ResourceFactory
{
    public static Resource FromJson(string json) => FromJObject(JObject.Parse(json));
    
    public static IEnumerable<Resource> FromListJson(string json)
    {
        var listResponse = JObject.Parse(json);
        return listResponse.SelectTokens("$.items[*]").Select(item => FromJObject((JObject)item));
    }
    
    public static Resource FromJObject(JObject data)
    {
        var kind = data.SelectToken("$.kind")?.Value<string>();
        return kind switch
        {
            "Deployment" => new Kubernetes.ResourceStatus.Resources.Deployment(data),
            "ReplicaSet" => new ReplicaSet(data),
            "Pod" => new Pod(data),
            _ => new Resource(data)
        };
    }
}