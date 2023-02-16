using System.Text;
using Newtonsoft.Json.Linq;

namespace Calamari.ResourceStatus.Resources;

public class Deployment : Resource
{
    public override string ChildKind => "ReplicaSet";

    public Deployment(JObject json) : base(json)
    {
    }

    public override (ResourceStatus, string) CheckStatus() => (ResourceStatus.Successful, "");

    public override string StatusToDisplay
    {
        get
        {
            var result = new StringBuilder();
            result.AppendLine($"Replicas: {Field("$.status.replicas")}");
            result.AppendLine($"Available: {Field("$.status.availableReplicas")}");
            result.AppendLine($"Ready: {Field("$.status.readyReplicas")}");
            result.AppendLine($"Up-to-date: {Field("$.status.updatedReplicas")}");
            return result.ToString();
        }
    }
}