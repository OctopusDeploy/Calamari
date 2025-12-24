using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Calamari.ArgoCD.Domain
{
    public class ApplicationStatus
    {
        [JsonPropertyName("sync")]
        public SyncStatus Sync { get; set; } = new SyncStatus();

        [JsonPropertyName("health")]
        public HealthStatus Health { get; set; } = new HealthStatus();

        // Always a list - handles both single sourceType and multiple sourceTypes
        [JsonPropertyName("sourceTypes")] 
        public List<SourceType> SourceTypes { get; set; } = new List<SourceType>();

        [JsonPropertyName("summary")]
        public StatusSummary Summary { get; set; } = new StatusSummary();

        // [JsonPropertyName("reconciledAt")]
        // public Instant ReconciledAt { get; set; } = Instant.MinValue;
    }

    public class SyncStatus
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class HealthStatus
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    public class StatusSummary
    {
        [JsonPropertyName("images")]
        public List<string> Images { get; set; } = new List<string>();
    }

    public enum SourceType
    {
        Directory,
        Helm,
        Kustomize,
        Plugin
    }
}
