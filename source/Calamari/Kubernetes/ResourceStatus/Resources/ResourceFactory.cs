using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public static class ResourceFactory
    {
        static Dictionary<ResourceGroupVersionKind, Func<JObject, Options, Resource>> resourceFactories = new Dictionary<ResourceGroupVersionKind, Func<JObject, Options, Resource>>
        {
            { SupportedResourceGroupVersionKinds.PodV1, (d, o) => new Pod(d, o) },
            { SupportedResourceGroupVersionKinds.ReplicaSetV1, (d, o) => new ReplicaSet(d, o) },
            { SupportedResourceGroupVersionKinds.DeploymentV1, (d, o) => new Deployment(d, o) },
            { SupportedResourceGroupVersionKinds.StatefulSetV1, (d, o) => new StatefulSet(d, o) },
            { SupportedResourceGroupVersionKinds.DaemonSetV1, (d, o) => new DaemonSet(d, o) },
            { SupportedResourceGroupVersionKinds.JobV1, (d, o) => new Job(d, o) },
            { SupportedResourceGroupVersionKinds.CronJobV1, (d, o) => new CronJob(d, o) },
            { SupportedResourceGroupVersionKinds.ServiceV1, (d, o) => new Service(d, o) },
            { SupportedResourceGroupVersionKinds.IngressV1, (d, o) => new Ingress(d, o) },
            { SupportedResourceGroupVersionKinds.EndpointSliceV1, (d, o) => new EndpointSlice(d, o) },
            { SupportedResourceGroupVersionKinds.ConfigMapV1, (d, o) => new ConfigMap(d, o) },
            { SupportedResourceGroupVersionKinds.SecretV1, (d, o) => new Secret(d, o) },
            { SupportedResourceGroupVersionKinds.PersistentVolumeClaimV1, (d, o) => new PersistentVolumeClaim(d, o) },
        };
        
        public static Resource FromJson(string json, Options options) => FromJObject(JObject.Parse(json), options);
        
        public static IEnumerable<Resource> FromListJson(string json, Options options)
        {
            var listResponse = JObject.Parse(json);
            return listResponse.SelectTokens("$.items[*]").Select(item => FromJObject((JObject)item, options));
        }
        
        public static Resource FromJObject(JObject data, Options options)
        {
            var gvk = data.ToResourceGroupVersionKind();
            return resourceFactories.ContainsKey(gvk) ? resourceFactories[gvk](data, options) : new Resource(data, options);
        }
    }
}