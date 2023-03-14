using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Service : Resource
    {
        public override string ChildKind => "EndpointSlice";
        
        public string ClusterIp { get; }

        public Service(JObject json) : base(json)
        {
            ClusterIp = Field("$.spec.clusterIP");
        }
    
        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Service>(lastStatus);
            return last.ClusterIp != ClusterIp;
        }
    }
}