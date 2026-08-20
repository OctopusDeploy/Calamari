using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class DaemonSet: Resource
    {
        public override ResourceGroupVersionKind ChildGroupVersionKind => SupportedResourceGroupVersionKinds.PodV1;

        public int Desired { get; }
        public int Current { get; }
        public int Ready { get; }
        public int UpToDate { get; }
        public int Available { get; }
        public string NodeSelector { get; }

        public DaemonSet(JObject json, Options options) : base(json, options)
        {
            Desired = FieldOrDefault(json, "$.status.desiredNumberScheduled", 0);
            Current = FieldOrDefault(json, "$.status.currentNumberScheduled", 0);
            Ready = FieldOrDefault(json, "$.status.numberReady", 0);
            UpToDate = FieldOrDefault(json, "$.status.updatedNumberScheduled", 0);
            Available = FieldOrDefault(json, "$.status.numberAvailable", 0);
            var selectors = json.SelectToken("$.spec.template.spec.nodeSelector")
                ?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
            NodeSelector = FormatNodeSelectors(selectors);

            ResourceStatus = options.EnableLegacyResourceStatusChecks
                ? GetLegacyResourceStatus()
                : GetResourceStatus(json);
        }

        ResourceStatus GetLegacyResourceStatus()
            => Available == Desired && UpToDate == Desired && Ready == Desired
                ? ResourceStatus.Successful
                : ResourceStatus.InProgress;

        // Aligns with gitops-engine getDaemonSetHealth.
        ResourceStatus GetResourceStatus(JObject json)
        {
            var generation = FieldOrDefault(json, "$.metadata.generation", 0);
            var observedGeneration = FieldOrDefault(json, "$.status.observedGeneration", 0);
            if (generation > observedGeneration)
            {
                return ResourceStatus.InProgress;
            }

            if (Field(json, "$.spec.updateStrategy.type") == "OnDelete")
            {
                return ResourceStatus.Successful;
            }

            if (UpToDate < Desired)
            {
                return ResourceStatus.InProgress;
            }

            if (Available < Desired)
            {
                return ResourceStatus.InProgress;
            }

            return ResourceStatus.Successful;
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<DaemonSet>(lastStatus);
            return last.Desired != Desired
                   || last.Current != Current
                   || last.Ready != Ready
                   || last.UpToDate != UpToDate
                   || last.Available != Available
                   || last.NodeSelector != NodeSelector;
        }

        private static string FormatNodeSelectors(Dictionary<string, string> nodeSelectors)
        {
            var selectors = nodeSelectors
                .ToList()
                .OrderBy(_ => _.Key)
                .ThenBy(_ => _.Value)
                .Select(_ => $"{_.Key}={_.Value}");
            return string.Join(",", selectors);
        }
    }
}

