using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class PersistentVolumeClaim: Resource
    {
        public string Status { get; }
        public string Volume { get; }
        public string Capacity { get; }
        public IEnumerable<string> AccessModes { get; }
        public string StorageClass { get; }
        
        public PersistentVolumeClaim(JObject json, Options options) : base(json, options)
        {
            Status = Field(json, "$.status.phase");
            Volume = Field(json, "$.spec.volumeName");
            Capacity = Field(json, "$.status.capacity.storage");
            AccessModes = json.SelectToken("$.status.accessModes")?.Values<string>().ToList() ?? new List<string>();
            StorageClass = Field(json, "$.spec.storageClassName");
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<PersistentVolumeClaim>(lastStatus);
            return last.Status != Status
                   || last.Volume != Volume
                   || last.Capacity != Capacity
                   || !last.AccessModes.SequenceEqual(AccessModes)
                   || last.StorageClass != StorageClass;
        }
    }
}

