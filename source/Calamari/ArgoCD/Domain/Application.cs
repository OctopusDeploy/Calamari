using System;
using System.Collections.Generic;
using System.Linq;
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

    public static class ApplicationExtensionMethods
    {
        public static IReadOnlyCollection<ApplicationSourceWithMetadata> GetSourcesWithMetadata(this Application application)
        {
            return application.Spec.Sources.Select((s, i) =>
                                                   {
                                                       var sourceType = i < application.Status.SourceTypes.Count ? application.Status.SourceTypes[i] : (SourceType?)null;
                                                       return new ApplicationSourceWithMetadata(s, sourceType, i);
                                          })
                              .ToArray();

        }
    }
    
    public class ApplicationSourceWithMetadata
    {
        public ApplicationSourceWithMetadata(ApplicationSource source, SourceType? sourceType, int index)
        {
            Source = source;
            SourceType = sourceType;
            Index = index;
        }

        public ApplicationSource Source { get; }
        
        public SourceType? SourceType { get; }

        public int Index { get; }
    }
}
