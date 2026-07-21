using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ReplicaSet : Resource
    {
        public override ResourceGroupVersionKind ChildGroupVersionKind => SupportedResourceGroupVersionKinds.PodV1;

        public int Desired { get; }
        public int Current { get; }
        public int Ready { get; }

        public ReplicaSet(JObject json, Options options) : base(json, options)
        {
            Desired = FieldOrDefault("$.status.replicas", 0);
            Current = FieldOrDefault($".status.availableReplicas", 0);
            Ready = FieldOrDefault("$.status.readyReplicas", 0);

            ResourceStatus = options.EnableLegacyResourceStatusChecks
                ? GetLegacyResourceStatus()
                : GetResourceStatus(json);
        }

        ResourceStatus GetLegacyResourceStatus()
            => Ready == Desired && Desired == Current ? ResourceStatus.Successful : ResourceStatus.InProgress;

        // Aligns with gitops-engine getReplicaSetHealth.
        ResourceStatus GetResourceStatus(JObject json)
        {
            var generation = FieldOrDefault("$.metadata.generation", 0);
            var observedGeneration = FieldOrDefault("$.status.observedGeneration", 0);
            if (generation > observedGeneration)
            {
                return ResourceStatus.InProgress;
            }

            var conditions = json.SelectToken("$.status.conditions") as JArray;
            var replicaFailure = conditions?.FirstOrDefault(c => c["type"]?.ToString() == "ReplicaFailure");
            if (replicaFailure != null && replicaFailure["status"]?.ToString() == "True")
            {
                return ResourceStatus.Failed;
            }

            var specReplicas = FieldOrDefault<int?>("$.spec.replicas", null);
            if (specReplicas.HasValue && Current < specReplicas.Value)
            {
                return ResourceStatus.InProgress;
            }

            return ResourceStatus.Successful;
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<ReplicaSet>(lastStatus);
            return last.Desired != Desired|| last.Ready != Ready || last.Current != Current;
        }
    }
}
