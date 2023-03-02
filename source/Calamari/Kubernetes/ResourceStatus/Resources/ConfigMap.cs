using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ConfigMap : Resource
    {
        public int Data { get; set; }
        
        public ConfigMap(JObject json, string cluster) : base(json, cluster)
        {
            Data = data.SelectTokens("$.data").Count();
        }
    }
}