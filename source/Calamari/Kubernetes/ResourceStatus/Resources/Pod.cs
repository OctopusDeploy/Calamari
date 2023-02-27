using System;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources;

public class Pod : Resource
{
    public string Phase { get; }
    public override ResourceStatus Status { get; }

    public Pod(JObject json) : base(json)
    {
        Phase = Field("$.status.phase");
        
        // TODO implement this
        Status = Phase switch
        {
            "Succeeded" or "Running" => ResourceStatus.Successful,
            "Pending" => ResourceStatus.InProgress,
            _ => ResourceStatus.Failed
        };
    }

    public override bool HasUpdate(Resource lastStatus)
    {
        var last = CastOrThrow<Pod>(lastStatus);
        return last.Phase != Phase;
    }
}