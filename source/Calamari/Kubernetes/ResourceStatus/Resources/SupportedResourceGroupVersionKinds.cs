using System;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public static class SupportedResourceGroupVersionKinds
    {
        public static ResourceGroupVersionKind PodV1 => new ResourceGroupVersionKind("", "v1", "Pod");
        public static ResourceGroupVersionKind ServiceV1 => new ResourceGroupVersionKind("", "v1", "Service");
        public static ResourceGroupVersionKind SecretV1 => new ResourceGroupVersionKind("", "v1", "Secret");
        public static ResourceGroupVersionKind ConfigMapV1 => new ResourceGroupVersionKind("", "v1", "ConfigMap");
        public static ResourceGroupVersionKind PersistentVolumeClaimV1 => new ResourceGroupVersionKind("", "v1", "PersistentVolumeClaim");
        
        public static ResourceGroupVersionKind ReplicaSetV1 => new ResourceGroupVersionKind("apps", "v1", "ReplicaSet");
        public static ResourceGroupVersionKind DeploymentV1 => new ResourceGroupVersionKind("apps", "v1", "Deployment");
        public static ResourceGroupVersionKind StatefulSetV1 => new ResourceGroupVersionKind("apps", "v1", "StatefulSet");
        public static ResourceGroupVersionKind DaemonSetV1 => new ResourceGroupVersionKind("apps", "v1", "DaemonSet");
        
        public static ResourceGroupVersionKind JobV1 => new ResourceGroupVersionKind("batch", "v1", "Job");
        public static ResourceGroupVersionKind CronJobV1 => new ResourceGroupVersionKind("batch", "v1", "CronJob");
        
        public static ResourceGroupVersionKind IngressV1 => new ResourceGroupVersionKind("networking.k8s.io", "v1", "Ingress");

        public static ResourceGroupVersionKind EndpointSliceV1 => new ResourceGroupVersionKind("discovery.k8s.io", "v1", "EndpointSlice");
    }
}