using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain.Converters
{
    public class ApplicationStatusConverter : JsonConverter<ApplicationStatus>
    {
        public override ApplicationStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var status = new ApplicationStatus();

            // Handle Sync
            if (root.TryGetProperty("sync", out var syncElement))
            {
                var sync = JsonSerializer.Deserialize<SyncStatus>(syncElement.GetRawText(), options);
                if (sync is { })
                {
                    status.Sync = sync;
                }
            }

            // Handle Health
            if (root.TryGetProperty("health", out var healthElement))
            {
                var health = JsonSerializer.Deserialize<HealthStatus>(healthElement.GetRawText(), options);
                if (health is { })
                {
                    status.Health = health;
                }
            }

            // Handle summary
            if (root.TryGetProperty("summary", out var summaryElement))
            {
                var summary = JsonSerializer.Deserialize<StatusSummary>(summaryElement.GetRawText(), options);
                if (summary is { })
                {
                    status.Summary = summary;
                }
            }

            // Handle sourceTypes - check both "sourceType" and "sourceTypes" properties
            if (root.TryGetProperty("sourceTypes", out var sourceTypesElement))
            {
                // Multiple sourceTypes case
                foreach (var typeElement in sourceTypesElement.EnumerateArray())
                {
                    var typeString = typeElement.GetString();
                    if (typeString is { } && Enum.TryParse<SourceType>(typeString, true, out var sourceType))
                    {
                        status.SourceTypes.Add(sourceType);
                    }
                }
            }
            else if (root.TryGetProperty("sourceType", out var sourceTypeElement))
            {
                // Single sourceType case
                var typeString = sourceTypeElement.GetString();
                if (typeString is { } && Enum.TryParse<SourceType>(typeString, true, out var sourceType))
                {
                    status.SourceTypes.Add(sourceType);
                }
            }

            // // Handle reconciledAt
            // if (root.TryGetProperty("reconciledAt", out var reconciledAtElement) && reconciledAtElement.ValueKind == JsonValueKind.String)
            // {
            //     var rawReconciledAt = reconciledAtElement.GetString();
            //     if (!string.IsNullOrWhiteSpace(rawReconciledAt))
            //     {
            //         status.ReconciledAt = InstantPattern.General.Parse(rawReconciledAt).Value;
            //     }
            // }

            return status;
        }

        public override void Write(Utf8JsonWriter writer, ApplicationStatus value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // Write summary
            writer.WritePropertyName("summary");
            JsonSerializer.Serialize(writer, value.Summary, options);

            // Write sourceTypes
            if (value.SourceTypes.Count > 0)
            {
                if (value.SourceTypes.Count == 1)
                {
                    writer.WritePropertyName("sourceType");
                    writer.WriteStringValue(value.SourceTypes[0].ToString());
                }
                else
                {
                    writer.WritePropertyName("sourceTypes");
                    writer.WriteStartArray();
                    foreach (var sourceType in value.SourceTypes)
                    {
                        writer.WriteStringValue(sourceType.ToString());
                    }
                    writer.WriteEndArray();
                }
            }

            writer.WriteEndObject();
        }
    }
}
