using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Deployment : Resource
    {
        public override string ChildKind => "ReplicaSet";
        
        public int UpToDate { get; }
        public string Ready { get; }
        public int Available { get; }
        public override ResourceStatus ResourceStatus { get; }

        public Deployment(JObject json) : base(json)
        {
            var readyReplicas = FieldOrDefault("$.status.readyReplicas", 0);
            var desiredReplicas = FieldOrDefault("$.spec.replicas", 0);
            var totalReplicas = FieldOrDefault("$.status.replicas", 0);
            Ready = $"{readyReplicas}/{desiredReplicas}";
            Available = FieldOrDefault("$.status.availableReplicas", 0);
            UpToDate = FieldOrDefault("$.status.updatedReplicas", 0);

            ResourceStatus = totalReplicas == desiredReplicas 
                             && UpToDate == desiredReplicas 
                             && Available == desiredReplicas 
                             && readyReplicas == desiredReplicas 
                ? ResourceStatus.Successful 
                : ResourceStatus.InProgress;
        }
    
        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Deployment>(lastStatus);
            return last.UpToDate != UpToDate 
                   || last.Ready != Ready 
                   || last.Available != Available;
        }
    }
}