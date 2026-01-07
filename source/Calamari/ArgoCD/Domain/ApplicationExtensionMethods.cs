using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Domain
{
    public static class ApplicationExtensionMethods
    {
        public static IReadOnlyCollection<ApplicationSourceWithMetadata> GetSourcesWithMetadata(this Application application)
        {
            var sourceTypesAreValid = application.Spec.Sources.Count == application.Status.SourceTypes.Count;
            return application.Spec.Sources.Select((s, i) =>
                                                   {
                                                       var sourceType = sourceTypesAreValid ? ParseToEnum(application.Status.SourceTypes[i]) : null;
                                                       return new ApplicationSourceWithMetadata(s, sourceType, i);
                                                   })
                              .ToArray();

        }

        static SourceType? ParseToEnum(string sourceTypeString)
        {
            if (!sourceTypeString.IsNullOrEmpty() && Enum.TryParse<SourceType>(sourceTypeString, true, out var sourceType))
            {
                return sourceType;
            }

            return null;
        }
    }
}