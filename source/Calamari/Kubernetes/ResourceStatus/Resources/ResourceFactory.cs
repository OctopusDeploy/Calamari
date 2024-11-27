using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public static class ResourceFactory
    {
        public static Resource FromJson(string json, Options options) => FromJObject(JObject.Parse(json), options);
        
        public static IEnumerable<Resource> FromListJson(string json, Options options)
        {
            var listResponse = JObject.Parse(json);
            return listResponse.SelectTokens("$.items[*]").Select(item => FromJObject((JObject)item, options));
        }
        
        public static Resource FromJObject(JObject data, Options options)
        {
            var gvk = data.ToResourceGroupVersionKind();
            
            switch (gvk.Kind)
            {   
                case "Pod": 
                    return new Pod(data, options);
                case "ReplicaSet": 
                    return new ReplicaSet(data, options);
                case "Deployment":
                    return new Deployment(data, options);
                case "StatefulSet":
                    return new StatefulSet(data, options);
                case "DaemonSet":
                    return new DaemonSet(data, options);
                case "Job":
                    return new Job(data, options);
                case "CronJob":
                    return new CronJob(data, options);
                case "Service": 
                    return new Service(data, options);
                case "Ingress":
                    return new Ingress(data, options);
                case "EndpointSlice": 
                    return new EndpointSlice(data, options); 
                case "ConfigMap":
                    return new ConfigMap(data, options);
                case "Secret":
                    return new Secret(data, options);
                case "PersistentVolumeClaim":
                    return new PersistentVolumeClaim(data, options);
                default:
                    return new Resource(data, options);
            }
        }
    }
}