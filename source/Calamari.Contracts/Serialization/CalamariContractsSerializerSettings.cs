using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Octopus.Calamari.Contracts.Serialization;

public static class CalamariContractsSerializerSettings
{
    public static JsonSerializerSettings Create() => new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        Converters = { new StringEnumConverter() }
    };
}
