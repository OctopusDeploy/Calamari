using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class StatefulSet: Resource
    {
        public override ResourceGroupVersionKind ChildGroupVersionKind => SupportedResourceGroupVersionKinds.PodV1;

        public string Ready { get; }

        public StatefulSet(JObject json, Options options) : base(json, options)
        {
            var readyReplicas = FieldOrDefault("$.status.readyReplicas", 0);
            var replicas = FieldOrDefault("$.status.replicas", 0);
            Ready = $"{readyReplicas}/{replicas}";

            ResourceStatus = options.EnableLegacyResourceStatusChecks
                ? GetLegacyResourceStatus(readyReplicas, replicas)
                : GetResourceStatus(readyReplicas);
        }

        static ResourceStatus GetLegacyResourceStatus(int readyReplicas, int replicas)
            => readyReplicas == replicas ? ResourceStatus.Successful : ResourceStatus.InProgress;

        // Aligns with gitops-engine getStatefulSetHealth.
        ResourceStatus GetResourceStatus(int readyReplicas)
        {
            var generation = FieldOrDefault("$.metadata.generation", 0);
            var observedGeneration = FieldOrDefault("$.status.observedGeneration", 0);
            if (observedGeneration == 0 || generation > observedGeneration)
            {
                return ResourceStatus.InProgress;
            }

            var specReplicas = FieldOrDefault<int?>("$.spec.replicas", null);
            if (specReplicas.HasValue && readyReplicas < specReplicas.Value)
            {
                return ResourceStatus.InProgress;
            }

            var updateStrategy = Field("$.spec.updateStrategy.type");
            if (updateStrategy == "RollingUpdate" && data.SelectToken("$.spec.updateStrategy.rollingUpdate") != null)
            {
                var partition = FieldOrDefault<int?>("$.spec.updateStrategy.rollingUpdate.partition", null);
                var updatedReplicas = FieldOrDefault("$.status.updatedReplicas", 0);
                if (specReplicas.HasValue && partition.HasValue && updatedReplicas < specReplicas.Value - partition.Value)
                {
                    return ResourceStatus.InProgress;
                }
                return ResourceStatus.Successful;
            }

            if (updateStrategy == "OnDelete")
            {
                return ResourceStatus.Successful;
            }

            if (Field("$.status.updateRevision") != Field("$.status.currentRevision"))
            {
                return ResourceStatus.InProgress;
            }

            return ResourceStatus.Successful;
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<StatefulSet>(lastStatus);
            return last.Ready != Ready;
        }
    }
}
