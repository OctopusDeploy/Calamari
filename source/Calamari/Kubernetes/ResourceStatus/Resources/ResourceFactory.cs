using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public static class ResourceFactory
    {
        public static Resource FromJson(string json, DeploymentContext context) => FromJObject(JObject.Parse(json), context);
        
        public static IEnumerable<Resource> FromListJson(string json, DeploymentContext context)
        {
            var listResponse = JObject.Parse(json);
            return listResponse.SelectTokens("$.items[*]").Select(item => FromJObject((JObject)item, context));
        }
        
        public static Resource FromJObject(JObject data, DeploymentContext context)
        {
            var kind = data.SelectToken("$.kind")?.Value<string>();
            switch (kind)
            {
                    case "Deployment":
                        return new Deployment(data, context);
                    case "ReplicaSet": 
                        return new ReplicaSet(data, context);
                    case "Pod": 
                        return new Pod(data, context);
                    case "Service": 
                        return new Service(data, context);
                    case "EndpointSlice": 
                        return new EndpointSlice(data, context); 
                    case "ConfigMap":
                        return new ConfigMap(data, context);
                    default:
                        return new Resource(data, context);
            }
        }
    }
}