using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources;

public class Service : Resource
{
    public Service(JObject json) : base(json)
    {
    }

    public override string ChildKind => "EndpointSlice";

    public override string StatusToDisplay => $"ClusterIP: {Field("$.spec.clusterIP")}";
}