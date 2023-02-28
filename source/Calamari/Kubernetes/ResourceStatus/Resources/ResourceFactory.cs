using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
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
            switch (kind)
            {
                    case "Deployment":
                        return new Deployment(data);
                    case "ReplicaSet": 
                        return new ReplicaSet(data);
                    case "Pod": 
                        return new Pod(data);
                    case "Service": 
                        return new Service(data);
                    case "EndpointSlice": 
                        return new EndpointSlice(data); 
                    default: 
                        return new Resource(data);
            }
        }
    }
}