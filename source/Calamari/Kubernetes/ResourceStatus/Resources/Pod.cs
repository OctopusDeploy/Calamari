using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources
{
    public class Pod : Resource
    {
        public string Phase { get; }
        public override ResourceStatus ResourceStatus { get; }
    
        public Pod(JObject json) : base(json)
        {
            Phase = Field("$.status.phase");
            
            // TODO implement this
            switch (Phase)
            {
                case"Succeeded": 
                case "Running":
                    ResourceStatus = ResourceStatus.Successful;
                    break;
                case "Pending":
                    ResourceStatus = ResourceStatus.InProgress;
                    break;
                default:
                    ResourceStatus = ResourceStatus.Failed;
                    break;
            }
        }
    
        public override bool HasUpdate(Resource lastStatus)
        {
            var last = CastOrThrow<Pod>(lastStatus);
            return last.Phase != Phase;
        }
    }
}