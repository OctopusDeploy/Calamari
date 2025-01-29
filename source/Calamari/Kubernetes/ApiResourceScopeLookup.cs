using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;

namespace Calamari.Kubernetes
{
    public interface IApiResourceScopeLookup
    {
        bool TryGetIsNamespaceScoped(ApiResourceIdentifier apiResourceIdentifier, out bool isNamespaceScoped);
    }

    public class ApiResourceScopeLookup : IApiResourceScopeLookup
    {
        readonly Kubectl kubectl;
        readonly ILog log;
        readonly Lazy<Dictionary<ApiResourceIdentifier, bool>> namespacedApiResourceDictionary;

        public ApiResourceScopeLookup(Kubectl kubectl, ILog log)
        {
            this.kubectl = kubectl;
            this.log = log;
            namespacedApiResourceDictionary = new Lazy<Dictionary<ApiResourceIdentifier, bool>>(GetNamespacedApiResourceDictionary);
        }

        public bool TryGetIsNamespaceScoped(ApiResourceIdentifier apiResourceIdentifier, out bool isNamespaceScoped)
        {
            return namespacedApiResourceDictionary.Value.TryGetValue(apiResourceIdentifier, out isNamespaceScoped);
        }

        Dictionary<ApiResourceIdentifier, bool> GetNamespacedApiResourceDictionary()
        {
            try
            {
                var apiResourceLines = kubectl.ExecuteCommandAndReturnOutput("api-resources", "-o", "wide");
                apiResourceLines.Result.VerifySuccess();

                return ApiResourceOutputParser.ParseKubectlApiResourceOutput(apiResourceLines.Output.InfoLogs.ToList());
            }
            catch
            {
                log.Warn("Unable to retrieve resource scoping using kubectl api-resources. Using default resource scopes.");
                return DefaultResourceScopeLookup;
            }
        }

        // This is the api resources of a k3s cluster running 1.30.0+k3s1
        // It's a decent stand-in for the default api resources of a k8s cluster
        //this is used if we can't get the information from the cluster
        internal static readonly Dictionary<ApiResourceIdentifier, bool> DefaultResourceScopeLookup = new Dictionary<ApiResourceIdentifier, bool>
        {
            { new ApiResourceIdentifier("v1", "Binding"), true },
            { new ApiResourceIdentifier("v1", "ComponentStatus"), false },
            { new ApiResourceIdentifier("v1", "ConfigMap"), true },
            { new ApiResourceIdentifier("v1", "Endpoints"), true },
            { new ApiResourceIdentifier("v1", "Event"), true },
            { new ApiResourceIdentifier("v1", "LimitRange"), true },
            { new ApiResourceIdentifier("v1", "Namespace"), false },
            { new ApiResourceIdentifier("v1", "Node"), false },
            { new ApiResourceIdentifier("v1", "PersistentVolumeClaim"), true },
            { new ApiResourceIdentifier("v1", "PersistentVolume"), false },
            { new ApiResourceIdentifier("v1", "Pod"), true },
            { new ApiResourceIdentifier("v1", "PodTemplate"), true },
            { new ApiResourceIdentifier("v1", "ReplicationController"), true },
            { new ApiResourceIdentifier("v1", "ResourceQuota"), true },
            { new ApiResourceIdentifier("v1", "Secret"), true },
            { new ApiResourceIdentifier("v1", "ServiceAccount"), true },
            { new ApiResourceIdentifier("v1", "Service"), true },
            { new ApiResourceIdentifier("admissionregistration.k8s.io/v1", "MutatingWebhookConfiguration"), false },
            { new ApiResourceIdentifier("admissionregistration.k8s.io/v1", "ValidatingAdmissionPolicy"), false },
            { new ApiResourceIdentifier("admissionregistration.k8s.io/v1", "ValidatingAdmissionPolicyBinding"), false },
            { new ApiResourceIdentifier("admissionregistration.k8s.io/v1", "ValidatingWebhookConfiguration"), false },
            { new ApiResourceIdentifier("apiextensions.k8s.io/v1", "CustomResourceDefinition"), false },
            { new ApiResourceIdentifier("apiregistration.k8s.io/v1", "APIService"), false },
            { new ApiResourceIdentifier("apps/v1", "ControllerRevision"), true },
            { new ApiResourceIdentifier("apps/v1", "DaemonSet"), true },
            { new ApiResourceIdentifier("apps/v1", "Deployment"), true },
            { new ApiResourceIdentifier("apps/v1", "ReplicaSet"), true },
            { new ApiResourceIdentifier("apps/v1", "StatefulSet"), true },
            { new ApiResourceIdentifier("authentication.k8s.io/v1", "SelfSubjectReview"), false },
            { new ApiResourceIdentifier("authentication.k8s.io/v1", "TokenReview"), false },
            { new ApiResourceIdentifier("authorization.k8s.io/v1", "LocalSubjectAccessReview"), true },
            { new ApiResourceIdentifier("authorization.k8s.io/v1", "SelfSubjectAccessReview"), false },
            { new ApiResourceIdentifier("authorization.k8s.io/v1", "SelfSubjectRulesReview"), false },
            { new ApiResourceIdentifier("authorization.k8s.io/v1", "SubjectAccessReview"), false },
            { new ApiResourceIdentifier("autoscaling/v2", "HorizontalPodAutoscaler"), true },
            { new ApiResourceIdentifier("batch/v1", "CronJob"), true },
            { new ApiResourceIdentifier("batch/v1", "Job"), true },
            { new ApiResourceIdentifier("certificates.k8s.io/v1", "CertificateSigningRequest"), false },
            { new ApiResourceIdentifier("coordination.k8s.io/v1", "Lease"), true },
            { new ApiResourceIdentifier("discovery.k8s.io/v1", "EndpointSlice"), true },
            { new ApiResourceIdentifier("events.k8s.io/v1", "Event"), true },
            { new ApiResourceIdentifier("flowcontrol.apiserver.k8s.io/v1", "FlowSchema"), false },
            { new ApiResourceIdentifier("flowcontrol.apiserver.k8s.io/v1", "PriorityLevelConfiguration"), false },
            { new ApiResourceIdentifier("helm.cattle.io/v1", "HelmChartConfig"), true },
            { new ApiResourceIdentifier("helm.cattle.io/v1", "HelmChart"), true },
            { new ApiResourceIdentifier("k3s.cattle.io/v1", "Addon"), true },
            { new ApiResourceIdentifier("k3s.cattle.io/v1", "ETCDSnapshotFile"), false },
            { new ApiResourceIdentifier("metrics.k8s.io/v1beta1", "NodeMetrics"), false },
            { new ApiResourceIdentifier("metrics.k8s.io/v1beta1", "PodMetrics"), true },
            { new ApiResourceIdentifier("networking.k8s.io/v1", "IngressClass"), false },
            { new ApiResourceIdentifier("networking.k8s.io/v1", "Ingress"), true },
            { new ApiResourceIdentifier("networking.k8s.io/v1", "NetworkPolicy"), true },
            { new ApiResourceIdentifier("node.k8s.io/v1", "RuntimeClass"), false },
            { new ApiResourceIdentifier("policy/v1", "PodDisruptionBudget"), true },
            { new ApiResourceIdentifier("rbac.authorization.k8s.io/v1", "ClusterRoleBinding"), false },
            { new ApiResourceIdentifier("rbac.authorization.k8s.io/v1", "ClusterRole"), false },
            { new ApiResourceIdentifier("rbac.authorization.k8s.io/v1", "RoleBinding"), true },
            { new ApiResourceIdentifier("rbac.authorization.k8s.io/v1", "Role"), true },
            { new ApiResourceIdentifier("scheduling.k8s.io/v1", "PriorityClass"), false },
            { new ApiResourceIdentifier("storage.k8s.io/v1", "CSIDriver"), false },
            { new ApiResourceIdentifier("storage.k8s.io/v1", "CSINode"), false },
            { new ApiResourceIdentifier("storage.k8s.io/v1", "CSIStorageCapacity"), true },
            { new ApiResourceIdentifier("storage.k8s.io/v1", "StorageClass"), false },
            { new ApiResourceIdentifier("storage.k8s.io/v1", "VolumeAttachment"), false }
        };
    }
}