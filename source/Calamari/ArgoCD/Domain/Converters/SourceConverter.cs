using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain.Converters
{
    public class SourceConverter : JsonConverter<ApplicationSource>
    {
        public override ApplicationSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;

            return JsonSerializer.Deserialize<ApplicationSource>(root.GetRawText(), options) ?? new ApplicationSource();
        }

        public override void Write(Utf8JsonWriter writer, ApplicationSource value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
