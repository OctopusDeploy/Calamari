using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Ingress : Resource
    {
        public string Class { get; set; }
        public IEnumerable<string> Hosts { get; set; }
        public string Address { get; set; }

        public Ingress(JObject json) : base(json)
        {
            Class = Field("$.spec.ingressClassName");

            var rules = data.SelectToken("$.spec.rules")
                ?.ToObject<IngressRule[]>() ?? new IngressRule[] { };
            Hosts = FormatHosts(rules);            

            var loadBalancerIngresses = data.SelectToken("$.status.loadBalancer.ingress")
                ?.ToObject<LoadBalancerIngress[]>() ?? new LoadBalancerIngress[] { };
            
            Address = FormatAddress(loadBalancerIngresses);
            
        }

        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Ingress>(lastStatus);
            return last.Class != Class
                   || !last.Hosts.SequenceEqual(Hosts)
                   || last.Address != Address;
        }
        
        private static string FormatAddress(IEnumerable<LoadBalancerIngress> loadBalancerIngresses)
        {
            return string.Join(',', loadBalancerIngresses.Select(ingress => ingress.Ip));
        }

        private static IEnumerable<string> FormatHosts(IEnumerable<IngressRule> rules)
        {
            return rules.Select(rule => rule.Host);
        }
    }
}

