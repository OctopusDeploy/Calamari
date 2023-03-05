using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ReplicaSet : Resource
    {
        public override string ChildKind => "Pod";
        
        public int Available { get; }
        public int Ready { get; }
        public int Replicas { get; }
    
        public override ResourceStatus Status { get; }
        
        public ReplicaSet(JObject json, DeploymentContext context) : base(json, context)
        {
            Replicas = FieldOrDefault("$.status.replicas", 0);
            Ready = FieldOrDefault("$.status.readyReplicas", 0);
            Available = FieldOrDefault($".status.availableReplicas", 0);
    
            if (Ready == Replicas && Available == Replicas)
            {
                Status = ResourceStatus.Successful;
            }
            else
            {
                Status = ResourceStatus.InProgress;
            }
        }
        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<ReplicaSet>(lastStatus);
            return last.Available != Available || last.Ready != Ready || last.Replicas != Replicas;
        }
    }
}