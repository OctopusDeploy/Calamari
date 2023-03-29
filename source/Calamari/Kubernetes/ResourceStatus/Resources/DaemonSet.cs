using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class DaemonSet: Resource
    {
        public override string ChildKind => "Pod";

        public int Desired { get; }
        public int Current { get; }
        public int Ready { get; }
        public int UpToDate { get; }
        public int Available { get; }
        public string NodeSelector { get; }
        public override ResourceStatus ResourceStatus { get; }

        public DaemonSet(JObject json) : base(json)
        {
            Desired = FieldOrDefault("$.status.desiredNumberScheduled", 0);
            Current = FieldOrDefault("$.status.currentNumberScheduled", 0);
            Ready = FieldOrDefault("$.status.numberReady", 0);
            UpToDate = FieldOrDefault("$.status.updatedNumberScheduled", 0);
            Available = FieldOrDefault("$.status.numberAvailable", 0);
            var selectors = data.SelectToken("$.spec.template.spec.nodeSelector")
                ?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
            NodeSelector = FormatNodeSelectors(selectors);

            ResourceStatus = Available == Desired && UpToDate == Desired && Ready == Desired
                ? ResourceStatus.Successful
                : ResourceStatus.InProgress;
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
            return string.Join(',', selectors);
        }
    }
}

