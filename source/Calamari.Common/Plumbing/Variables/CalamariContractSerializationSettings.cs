using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Calamari.Common.Plumbing.Variables;

public static class CalamariContractSerializationSettings
{

    // NOTE: Values here should match Octopus.Variables/CalamariContractSerializationSettings.cs
    // changes in one should be reflected in the other
    public static JsonSerializerSettings Default => new()
    {
        Formatting = Formatting.None,
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };
}