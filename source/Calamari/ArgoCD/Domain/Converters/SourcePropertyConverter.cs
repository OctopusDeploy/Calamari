using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain.Converters
{
    public class SourcePropertyConverter : JsonConverter<ApplicationSpec>
    {
        public override ApplicationSpec Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var spec = new ApplicationSpec();

            // Handle destination
            if (root.TryGetProperty("destination", out var destElement))
            {
                var destination = JsonSerializer.Deserialize<Destination>(destElement.GetRawText(), options);
                if (destination is { })
                {
                    spec.Destination = destination;
                }
            }

            // Handle project
            if (root.TryGetProperty("project", out var projectElement))
            {
                var project = projectElement.GetString();
                if (project is { })
                {
                    spec.Project = project;
                }
            }

            // Handle sources - check both "source" and "sources" properties
            if (root.TryGetProperty("sources", out var sourcesElement))
            {
                // Multiple sources case
                foreach (var sourceElement in sourcesElement.EnumerateArray())
                {
                    var source = DeserializeSource(sourceElement, options);
                    if (source is { })
                    {
                        spec.Sources.Add(source);
                    }
                }
            }
            else if (root.TryGetProperty("source", out var sourceElement))
            {
                // Single source case
                var source = DeserializeSource(sourceElement, options);
                if (source is { })
                {
                    spec.Sources.Add(source);
                }
            }

            return spec;
        }

        static ApplicationSource DeserializeSource(JsonElement sourceElement, JsonSerializerOptions options)
        {
            return JsonSerializer.Deserialize<ApplicationSource>(sourceElement.GetRawText(), options);
        }

        public override void Write(Utf8JsonWriter writer, ApplicationSpec value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // Write destination
            writer.WritePropertyName("destination");
            JsonSerializer.Serialize(writer, value.Destination, options);

            // Write project
            writer.WritePropertyName("project");
            writer.WriteStringValue(value.Project);

            // Write sources
            if (value.Sources.Count > 0)
            {
                if (value.Sources.Count == 1)
                {
                    writer.WritePropertyName("source");
                    JsonSerializer.Serialize(writer, value.Sources[0], value.Sources[0].GetType(), options);
                }
                else
                {
                    writer.WritePropertyName("sources");
                    writer.WriteStartArray();
                    foreach (var source in value.Sources)
                    {
                        JsonSerializer.Serialize(writer, source, source.GetType(), options);
                    }
                    writer.WriteEndArray();
                }
            }

            writer.WriteEndObject();
        }
    }
}
