using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Deployment : Resource
    {
        public override ResourceGroupVersionKind ChildGroupVersionKind => SupportedResourceGroupVersionKinds.ReplicaSetV1;

        readonly bool enableLegacyResourceStatusChecks;

        public int UpToDate { get; }
        public string Ready { get; }
        public int Available { get; }

        [JsonIgnore]
        public int Desired { get; }
        [JsonIgnore]
        public int TotalReplicas { get; }
        [JsonIgnore]
        public int ReadyReplicas { get; }

        public Deployment(JObject json, Options options) : base(json, options)
        {
            enableLegacyResourceStatusChecks = options.EnableLegacyResourceStatusChecks;

            ReadyReplicas = FieldOrDefault("$.status.readyReplicas", 0);
            Desired = FieldOrDefault("$.spec.replicas", 0);
            TotalReplicas = FieldOrDefault("$.status.replicas", 0);
            Ready = $"{ReadyReplicas}/{Desired}";
            Available = FieldOrDefault("$.status.availableReplicas", 0);
            UpToDate = FieldOrDefault("$.status.updatedReplicas", 0);

            ResourceStatus = GetResourceStatus(json);
        }

        // Deployment status logic aligns with ArgoCD's gitops-engine, which is also used in the Kubernetes Monitor.
        ResourceStatus GetResourceStatus(JObject json)
        {
            // gitops-engine treats a paused deployment as suspended rather than blocking on it.
            if (!enableLegacyResourceStatusChecks && FieldOrDefault("$.spec.paused", false))
            {
                return ResourceStatus.Successful;
            }

            var generation = FieldOrDefault("$.metadata.generation", 0);
            var observedGeneration = FieldOrDefault("$.status.observedGeneration", 0);

            if (generation > observedGeneration)
            {
                return ResourceStatus.InProgress;
            }

            var conditions = json.SelectToken("$.status.conditions") as JArray;
            var progressingCondition = conditions?.FirstOrDefault(c => c["type"]?.ToString() == "Progressing");
            if (progressingCondition != null && progressingCondition["reason"]?.ToString() == "ProgressDeadlineExceeded")
            {
                return ResourceStatus.Failed;
            }

            if (Desired > 0 && UpToDate < Desired)
            {
                return ResourceStatus.InProgress;
            }

            if (TotalReplicas > UpToDate)
            {
                return ResourceStatus.InProgress;
            }

            if (Available < UpToDate)
            {
                return ResourceStatus.InProgress;
            }

            return ResourceStatus.Successful;
        }

        public override void UpdateChildren(IEnumerable<Resource> children)
        {
            base.UpdateChildren(children);

            // This legacy check races against autoscalers (e.g. HPA) that adjust spec.replicas during a deployment
            // Remove once we're happy to push out fully
            if (!enableLegacyResourceStatusChecks)
            {
                return;
            }

            var foundReplicas = Children
                ?.SelectMany(child => child?.Children ?? Enumerable.Empty<Resource>())
                .Count() ?? 0;
            ResourceStatus = foundReplicas != Desired ? ResourceStatus.InProgress : ResourceStatus;
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Deployment>(lastStatus);
            return last.ResourceStatus != ResourceStatus
                || last.UpToDate != UpToDate
                || last.Ready != Ready
                || last.Available != Available;
        }
    }
}
