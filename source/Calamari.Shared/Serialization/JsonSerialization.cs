using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Calamari.Serialization
{

    /// <summary>
    /// Support for reading and writing JSON, exposed for convenience of those using JSON.NET.
    /// </summary>
    public static class JsonSerialization
    {
        /// <summary>
        /// The serializer settings used by Octopus when reading and writing JSON from the
        /// Octopus Deploy RESTful API.
        /// </summary>
        public static JsonSerializerSettings GetDefaultSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                Converters = new JsonConverterCollection
                {
                    new StringEnumConverter(),
                    new IsoDateTimeConverter {DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffK"}
                }
            };
        }
    }
}
