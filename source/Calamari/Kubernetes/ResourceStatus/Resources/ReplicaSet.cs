using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ReplicaSet : Resource
    {
        public override string ChildKind => "Pod";
        
        public int Desired { get; }
        public int Current { get; }
        public int Ready { get; }

        public ReplicaSet(JObject json, Options options) : base(json, options)
        {
            Desired = FieldOrDefault("$.status.replicas", 0);
            Current = FieldOrDefault($".status.availableReplicas", 0);
            Ready = FieldOrDefault("$.status.readyReplicas", 0);
            
            if (Ready == Desired && Desired == Current)
            {
                ResourceStatus = ResourceStatus.Successful;
            }
            else
            {
                ResourceStatus = ResourceStatus.InProgress;
            }
        }
        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<ReplicaSet>(lastStatus);
            return last.Desired != Desired|| last.Ready != Ready || last.Current != Current;
        }
    }
}