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

