using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Octopus.Calamari.Contracts.Serialization;

public static class JsonSerializationSettings
{
    public static JsonSerializerSettings NewtonsoftSerializationSettings =>
        new()
        {
            Formatting = Formatting.None,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            TypeNameHandling = TypeNameHandling.None,
            DateParseHandling = DateParseHandling.None,
        };
}