using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD.Conventions
{
    static class ScopingAnnotationReader
    {
        public static AnnotationScope GetScopeForApplicationSource(ApplicationSourceName? sourceName, IReadOnlyDictionary<string, string> applicationAnnotations, bool containsMultipleSources)
        {
            //If we have multiple sources, scoping annotations can only match named sources
            if (containsMultipleSources && sourceName == null)
            {
                return new AnnotationScope(null, null, null);
            }

            applicationAnnotations.TryGetValue(ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(sourceName), out var projectAnnotation);
            applicationAnnotations.TryGetValue(ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(sourceName), out var environmentAnnotation);
            applicationAnnotations.TryGetValue(ArgoCDConstants.Annotations.OctopusTenantAnnotationKey(sourceName), out var tenantAnnotation);
            
            return new AnnotationScope(
                                       projectAnnotation.ToProjectSlug(), 
                                       environmentAnnotation.ToEnvironmentSlug(),
                                       tenantAnnotation.ToTenantSlug()
                                      );
        }
        
        public static IReadOnlyCollection<string> GetImageReplacePathsForApplicationSource(ApplicationSourceName? sourceName, IReadOnlyDictionary<string, string> applicationAnnotations, bool containsMultipleSources)
        {
            //If we have multiple sources, scoping annotations can only match named sources
            if (containsMultipleSources && sourceName == null)
            {
                return new List<string>();
            }

            applicationAnnotations.TryGetValue(ArgoCDConstants.Annotations.OctopusImageReplacementPathsKey(sourceName), out var imagePathsAnnotation);
            
            return imagePathsAnnotation?.Split(',').Select(a => a.Trim()).ToList() ?? new  List<string>();
        }
    }
}
