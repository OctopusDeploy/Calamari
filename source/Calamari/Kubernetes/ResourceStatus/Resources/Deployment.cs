using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Deployment : Resource
    {
        public override string ChildKind => "ReplicaSet";
        
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
            ReadyReplicas = FieldOrDefault("$.status.readyReplicas", 0);
            Desired = FieldOrDefault("$.spec.replicas", 0);
            TotalReplicas = FieldOrDefault("$.status.replicas", 0);
            Ready = $"{ReadyReplicas}/{Desired}";
            Available = FieldOrDefault("$.status.availableReplicas", 0);
            UpToDate = FieldOrDefault("$.status.updatedReplicas", 0);
            
            ResourceStatus = TotalReplicas == Desired
                             && UpToDate == Desired
                             && Available == Desired 
                             && ReadyReplicas == Desired
                ? ResourceStatus.Successful 
                : ResourceStatus.InProgress;
        }

        public override void UpdateChildren(IEnumerable<Resource> children)
        {
            base.UpdateChildren(children);
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