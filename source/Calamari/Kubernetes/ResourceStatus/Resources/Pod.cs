using System.Text;
using Newtonsoft.Json.Linq;

namespace Calamari.ResourceStatus.Resources;

public class Pod : Resource
{
    public Pod(JObject json) : base(json)
    {
    }

    public override (ResourceStatus, string) CheckStatus()
    {
        var phase = Field("$.status.phase");
        return phase switch
        {
            "Running" => (ResourceStatus.Successful, "The Pod is running"),
            "Completed" => (ResourceStatus.Successful, "The Pod has completed"),
            "Pending" => (ResourceStatus.InProgress, "The Pod is in a pending state"),
            _ => (ResourceStatus.Failed, $"The Pod has failed with a state of {phase}")
        };
    }
    
    public override string StatusToDisplay => $"Status: {Field("$.status.phase")}";
}