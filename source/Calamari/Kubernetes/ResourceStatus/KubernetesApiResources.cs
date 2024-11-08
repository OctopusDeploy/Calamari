using System;
using System.Collections.Generic;

namespace Calamari.Kubernetes.ResourceStatus
{
    public static class KubernetesApiResources
    {
        public static readonly HashSet<string> NonNamespacedKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ComponentStatus",
            "Namespace",
            "Node",
            "PersistentVolume",
            "MutatingWebhookConfiguration",
            "ValidatingWebhookConfiguration",
            "CustomResourceDefinition",
            "APIService",
            "SelfSubjectReview",
            "TokenReview",
            "SelfSubjectAccessReview",
            "SelfSubjectRulesReview",
            "SubjectAccessReview",
            "CertificateSigningRequest",
            "FlowSchema",
            "PriorityLevelConfiguration",
            "IngressClass",
            "RuntimeClass",
            "ClusterRoleBinding",
            "ClusterRole",
            "PriorityClass",
            "CSIDriver",
            "CSINode",
            "StorageClass",
            "VolumeAttachment"
        };
    }
}