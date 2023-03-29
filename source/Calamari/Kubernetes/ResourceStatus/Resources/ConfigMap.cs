using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ConfigMap : Resource
    {
        public int Data { get; }
        
        public ConfigMap(JObject json) : base(json)
        {
            Data = (data.SelectToken("$.data")
                ?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>())
                .Count;
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<ConfigMap>(lastStatus);
            return last.Data != Data;
        }
    }
}