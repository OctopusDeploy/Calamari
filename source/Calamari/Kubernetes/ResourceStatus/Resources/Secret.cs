using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Secret: Resource
    {
        public int Data { get; }
        public string Type { get; }
        
        public Secret(JObject json) : base(json)
        {
            Type = Field("$.type");
            Data = (data.SelectToken("$.data")
                ?.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>())
                .Count;
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Secret>(lastStatus);
            return last.Data != Data || last.Type != Type;
        }
    }
}

