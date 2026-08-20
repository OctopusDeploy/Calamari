using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class StatefulSet: Resource
    {
        public override ResourceGroupVersionKind ChildGroupVersionKind => SupportedResourceGroupVersionKinds.PodV1;

        public string Ready { get; }

        public StatefulSet(JObject json, Options options) : base(json, options)
        {
            var readyReplicas = FieldOrDefault(json, "$.status.readyReplicas", 0);
            var replicas = FieldOrDefault(json, "$.status.replicas", 0);
            Ready = $"{readyReplicas}/{replicas}";

            ResourceStatus = options.EnableLegacyResourceStatusChecks
                ? GetLegacyResourceStatus(readyReplicas, replicas)
                : GetResourceStatus(json, readyReplicas);
        }

        static ResourceStatus GetLegacyResourceStatus(int readyReplicas, int replicas)
            => readyReplicas == replicas ? ResourceStatus.Successful : ResourceStatus.InProgress;

        // Aligns with gitops-engine getStatefulSetHealth.
        static ResourceStatus GetResourceStatus(JObject json, int readyReplicas)
        {
            var generation = FieldOrDefault(json, "$.metadata.generation", 0);
            var observedGeneration = FieldOrDefault(json, "$.status.observedGeneration", 0);
            if (observedGeneration == 0 || generation > observedGeneration)
            {
                return ResourceStatus.InProgress;
            }

            var specReplicas = FieldOrDefault<int?>(json, "$.spec.replicas", null);
            if (specReplicas.HasValue && readyReplicas < specReplicas.Value)
            {
                return ResourceStatus.InProgress;
            }

            var updateStrategy = Field(json, "$.spec.updateStrategy.type");
            if (updateStrategy == "RollingUpdate" && json.SelectToken("$.spec.updateStrategy.rollingUpdate") != null)
            {
                var partition = FieldOrDefault<int?>(json, "$.spec.updateStrategy.rollingUpdate.partition", null);
                var updatedReplicas = FieldOrDefault(json, "$.status.updatedReplicas", 0);
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

            if (Field(json, "$.status.updateRevision") != Field(json, "$.status.currentRevision"))
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
