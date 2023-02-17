using System.Linq;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus.Resources;

public class EndpointSlice : Resource
{
    public EndpointSlice(JObject json) : base(json)
    {
    }

    public override string StatusToDisplay =>
        Data.SelectTokens("$.endpoints[*].addresses[0]")
            .Select(address => $"- {address.Value<string>()}\n")
            .Aggregate("", (acc, cur) => acc + cur);
}