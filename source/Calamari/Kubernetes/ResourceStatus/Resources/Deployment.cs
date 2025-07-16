using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Deployment : Resource
    {
        public override ResourceGroupVersionKind ChildGroupVersionKind => SupportedResourceGroupVersionKinds.ReplicaSetV1;

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
            
            var generation = FieldOrDefault("$.metadata.generation", 0);
            var observedGeneration = FieldOrDefault("$.status.observedGeneration", 0);
            
            // Note that deployment status logic aligns with ArgoCD's gitops-engine, which is also used in the Kubernetes Monitor
            if (generation <= observedGeneration)
            {
                var conditions = json.SelectToken("$.status.conditions") as JArray;
                var progressingCondition = conditions?.FirstOrDefault(c => c["type"]?.ToString() == "Progressing");
                
                if (progressingCondition != null && progressingCondition["reason"]?.ToString() == "ProgressDeadlineExceeded")
                {
                    ResourceStatus = ResourceStatus.Failed;
                }
                else if (Desired > 0 && UpToDate < Desired)
                {
                    ResourceStatus = ResourceStatus.InProgress;
                }
                else if (TotalReplicas > UpToDate)
                {
                    ResourceStatus = ResourceStatus.InProgress;
                }
                else if (Available < UpToDate)
                {
                    ResourceStatus = ResourceStatus.InProgress;
                }
                else
                {
                    ResourceStatus = ResourceStatus.Successful;
                }
            }
            else
            {
                ResourceStatus = ResourceStatus.InProgress;
            }
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