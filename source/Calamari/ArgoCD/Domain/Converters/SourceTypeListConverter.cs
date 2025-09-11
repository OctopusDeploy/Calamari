using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain.Converters
{
    public class SourceTypeListConverter : JsonConverter<List<SourceType>>
    {
        public override List<SourceType> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException();
            }
            var list = new List<SourceType>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }
                
                if (reader.TokenType == JsonTokenType.String)
                {
                    var value = reader.GetString();
                    if (value is { } && Enum.TryParse<SourceType>(value, true, out var sourceType))
                    {
                        list.Add(sourceType);
                    }
                }
            }
            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<SourceType> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var item in value)
            {
                writer.WriteStringValue(item.ToString());
            }
            writer.WriteEndArray();
        }
    }
}
