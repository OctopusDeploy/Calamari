using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Service : Resource
    {
        public override string ChildKind => "EndpointSlice";
        
        public string ClusterIp { get; }
        
        // There isn't really failed or in-progress state for a Service
        public override ResourceStatus Status => ResourceStatus.Successful;
    
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