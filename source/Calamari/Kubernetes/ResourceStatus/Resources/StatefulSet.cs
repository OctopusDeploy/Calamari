using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class StatefulSet: Resource
    {
        public override string ChildKind => "Pod";

        public string Ready { get; }

        public StatefulSet(JObject json, Options options) : base(json, options)
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

