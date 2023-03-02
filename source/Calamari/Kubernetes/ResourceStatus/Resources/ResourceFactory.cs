using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public static class ResourceFactory
    {
        public static Resource FromJson(string json, string cluster, string actionId) => FromJObject(JObject.Parse(json), cluster, actionId);
        
        public static IEnumerable<Resource> FromListJson(string json, string cluster, string actionId)
        {
            var listResponse = JObject.Parse(json);
            return listResponse.SelectTokens("$.items[*]").Select(item => FromJObject((JObject)item, cluster, actionId));
        }
        
        public static Resource FromJObject(JObject data, string cluster, string actionId)
        {
            var kind = data.SelectToken("$.kind")?.Value<string>();
            switch (kind)
            {
                    case "Deployment":
                        return new Deployment(data, cluster, actionId);
                    case "ReplicaSet": 
                        return new ReplicaSet(data, cluster, actionId);
                    case "Pod": 
                        return new Pod(data, cluster, actionId);
                    case "Service": 
                        return new Service(data, cluster, actionId);
                    case "EndpointSlice": 
                        return new EndpointSlice(data, cluster, actionId); 
                    case "ConfigMap":
                        return new ConfigMap(data, cluster, actionId);
                    default:
                        return new Resource(data, cluster, actionId);
            }
        }
    }
}