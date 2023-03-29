using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class PersistentVolumeClaim: Resource
    {
        public string Status { get; set; }
        public string Volume { get; set; }
        public string Capacity { get; set; }
        public IEnumerable<string> AccessModes { get; set; }
        public string StorageClass { get; set; }
        
        public PersistentVolumeClaim(JObject json) : base(json)
        {
            Status = Field("$.status.phase");
            Volume = Field("$.spec.volumeName");
            Capacity = Field("$.status.capacity.storage");
            AccessModes = data.SelectToken("$.status.accessModes")?.Values<string>() ?? new string[] { };
            StorageClass = Field("$.spec.storageClassName");
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

