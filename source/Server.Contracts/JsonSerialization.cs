using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Sashimi.Server.Contracts
{
    public static class JsonSerialization
    {
        /// <summary>
        /// The serializer settings used by when reading and writing JSON from the
        /// Octopus Deploy RESTful API.
        /// </summary>
        public static JsonSerializerSettings GetDefaultSerializerSettings()
        {
            return new()
            {
                Formatting = Formatting.Indented,
                Converters = new JsonConverterCollection
                {
                    new StringEnumConverter(),
                    new IsoDateTimeConverter { DateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffK" }
                }
            };
        }
    }
}