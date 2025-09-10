using System;
using System.Text.Json.Serialization;
using Calamari.ArgoCD.Domain.Converters;

namespace Calamari.ArgoCD.Domain
{
    public class Application
    {
        [JsonPropertyName("metadata")]
        public Metadata Metadata { get; set; } = new Metadata();

        [JsonPropertyName("spec")]
        [JsonConverter(typeof(SourcePropertyConverter))]
        public ApplicationSpec Spec { get; set; } = new ApplicationSpec();

        [JsonPropertyName("status")]
        [JsonConverter(typeof(ApplicationStatusConverter))]
        public ApplicationStatus Status { get; set; } = new ApplicationStatus();
        
    }
}
