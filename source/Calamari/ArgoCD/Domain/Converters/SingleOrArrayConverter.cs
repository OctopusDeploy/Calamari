using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain.Converters
{
    public class SingleOrArrayConverter<T> : JsonConverter<List<T>>
    {
        public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var result = new List<T>();

            if (reader.TokenType == JsonTokenType.StartArray)
            {
                // Handle array case
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        break;
                    }

                    var item = JsonSerializer.Deserialize<T>(ref reader, options);
                    if (item is { })
                    {
                        result.Add(item);
                    }
                }
            }
            else
            {
                // Handle single value case
                var item = JsonSerializer.Deserialize<T>(ref reader, options);
                if (item is { })
                {
                    result.Add(item);
                }
            }

            return result;
        }

        public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
        {
            if (value.Count == 0)
            {
                writer.WriteNullValue();
                return;
            }

            if (value.Count == 1)
            {
                // Write as single value
                JsonSerializer.Serialize(writer, value[0], options);
            }
            else
            {
                // Write as array
                writer.WriteStartArray();
                foreach (var item in value)
                {
                    JsonSerializer.Serialize(writer, item, options);
                }
                writer.WriteEndArray();
            }
        }
    }
}
