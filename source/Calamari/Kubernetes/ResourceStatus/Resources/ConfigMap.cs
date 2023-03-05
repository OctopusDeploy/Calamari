using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class ConfigMap : Resource
    {
        public int Data { get; set; }
        
        public ConfigMap(JObject json, DeploymentContext context) : base(json, context)
        {
            Data = data.SelectTokens("$.data").Count();
        }
    }
}