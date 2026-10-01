#nullable enable
using k8s;
using k8s.Models;

namespace Calamari.ArgoCD.Models
{
    /// <summary>
    /// Represents an Argo Rollout custom resource (argoproj.io/v1alpha1, kind: Rollout).
    /// Only the fields needed for image replacement are modelled here.
    /// </summary>
    [KubernetesEntity(ApiVersion = "v1alpha1", Group = "argoproj.io", Kind = "Rollout", PluralName = "rollouts")]
    public class V1alpha1Rollout : IKubernetesObject<V1ObjectMeta>, IMetadata<V1ObjectMeta>
    {
        public string ApiVersion { get; set; } = "argoproj.io/v1alpha1";
        public string Kind { get; set; } = "Rollout";
        public V1ObjectMeta Metadata { get; set; } = new();
        public V1alpha1RolloutSpec? Spec { get; set; }
    }

    public class V1alpha1RolloutSpec
    {
        public V1PodTemplateSpec? Template { get; set; }
    }
}
