using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public static class ResourceFactory
    {
        public static Resource FromJson(string json, string cluster) => FromJObject(JObject.Parse(json), cluster);
        
        public static IEnumerable<Resource> FromListJson(string json, string cluster)
        {
            var listResponse = JObject.Parse(json);
            return listResponse.SelectTokens("$.items[*]").Select(item => FromJObject((JObject)item, cluster));
        }
        
        public static Resource FromJObject(JObject data, string cluster)
        {
            var kind = data.SelectToken("$.kind")?.Value<string>();
            switch (kind)
            {
                    case "Deployment":
                        return new Deployment(data, cluster);
                    case "ReplicaSet": 
                        return new ReplicaSet(data, cluster);
                    case "Pod": 
                        return new Pod(data, cluster);
                    case "Service": 
                        return new Service(data, cluster);
                    case "EndpointSlice": 
                        return new EndpointSlice(data, cluster); 
                    case "ConfigMap":
                        return new ConfigMap(data, cluster);
                    default:
                        return new Resource(data, cluster);
            }
        }
    }
}