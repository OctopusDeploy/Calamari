using Newtonsoft.Json.Linq;

namespace Calamari.ResourceStatus.Resources;

public class Deployment : Resource
{
    public override string ChildKind => "ReplicaSet";

    public Deployment(JObject json) : base(json)
    {
    }

    public override (ResourceStatus, string) CheckStatus() => (ResourceStatus.Successful, "");
}