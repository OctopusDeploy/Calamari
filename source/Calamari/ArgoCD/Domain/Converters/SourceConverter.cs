using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain.Converters
{
    public class SourceConverter : JsonConverter<SourceBase>
    {
        public override SourceBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;

            // Determine type based on properties
            if (root.TryGetProperty("helm", out _))
            {
                return JsonSerializer.Deserialize<HelmSource>(root.GetRawText(), options) ?? new HelmSource();
            }

            if (root.TryGetProperty("ref", out _))
            {
                return JsonSerializer.Deserialize<ReferenceSource>(root.GetRawText(), options) ?? new ReferenceSource();
            }

            return JsonSerializer.Deserialize<BasicSource>(root.GetRawText(), options) ?? new BasicSource();
        }

        public override void Write(Utf8JsonWriter writer, SourceBase value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
