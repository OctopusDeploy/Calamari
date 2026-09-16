using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class PersistentVolume : Resource
    {
        public string Status { get; }

        public string ReclaimPolicy { get; }

        public string Capacity { get; }

        public PersistentVolume(JObject json, Options options) : base(json, options)
        {
            Status = Field(json, "$.status.phase");
            ReclaimPolicy = Field(json, "$.spec.persistentVolumeReclaimPolicy");
            Capacity = Field(json, "$.spec.capacity.storage");
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<PersistentVolume>(lastStatus);
            return last.Status != Status && last.ReclaimPolicy != ReclaimPolicy && last.Capacity != Capacity;
        }
    }
}