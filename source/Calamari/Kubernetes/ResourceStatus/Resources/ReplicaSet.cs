using System.Text;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources;

public class ReplicaSet : Resource
{
    public override string ChildKind => "Pod";

    public ReplicaSet(JObject json) : base(json)
    {
    }

    public override (ResourceStatus, string) CheckStatus() => (ResourceStatus.Successful, "");
    
    public override string StatusToDisplay
    {
        get
        {
            var result = new StringBuilder();
            result.AppendLine($"Replicas: {Field("$.status.replicas")}");
            result.AppendLine($"Ready: {Field("$.status.readyReplicas")}");
            result.AppendLine($"Available: {Field("$.status.availableReplicas")}");
            return result.ToString();
        }
    }
}