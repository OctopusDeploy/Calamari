using Newtonsoft.Json.Linq;

namespace Calamari.ResourceStatus.Resources;

public class ReplicaSet : Resource
{
    public override string ChildKind => "Pod";

    public ReplicaSet(JObject json) : base(json)
    {
    }

    public override (ResourceStatus, string) CheckStatus() => (ResourceStatus.Successful, "");
}