using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class StatefulSet: Resource
    {
        public override string ChildKind => "Pod";

        public string Ready { get; }
        public override ResourceStatus ResourceStatus { get; }

        public StatefulSet(JObject json) : base(json)
        {
            var readyReplicas = FieldOrDefault("$.status.readyReplicas", 0);
            var replicas = FieldOrDefault("$.status.replicas", 0);
            Ready = $"{readyReplicas}/{replicas}";

            ResourceStatus = readyReplicas == replicas ? ResourceStatus.Successful : ResourceStatus.InProgress;
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<StatefulSet>(lastStatus);
            return last.Ready != Ready;
        }
    }
}

