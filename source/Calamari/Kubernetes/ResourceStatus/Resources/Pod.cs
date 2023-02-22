using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources;

public class Pod : Resource
{
    public string Phase { get; }
    public override ResourceStatus Status { get; }

    public Pod(JObject json) : base(json)
    {
        Phase = Field("$.status.phase");
        
        Status = ResourceStatus.Successful;
    }

    public override bool HasUpdate(Resource lastStatus)
    {
        var last = CastOrThrow<Pod>(lastStatus);
        return last.Phase != Phase;
    }
}