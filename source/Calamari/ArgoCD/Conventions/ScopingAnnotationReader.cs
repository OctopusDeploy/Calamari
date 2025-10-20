#if NET
using System;
using System.Collections.Generic;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.ArgoCD.Conventions
{
    static class ScopingAnnotationReader
    {
        public static (ProjectSlug? Project, EnvironmentSlug? Environment, TenantSlug? Tenant) GetScopeForApplicationSource(ApplicationSourceName? sourceName, IReadOnlyDictionary<string, string> applicationAnnotations, bool containsMultipleSources)
        {
            //If we have multiple sources, scoping annotations can only match named sources
            if (containsMultipleSources && sourceName == null)
            {
                return (null, null, null);
            }

            applicationAnnotations.TryGetValue(ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(sourceName), out var projectAnnotation);
            applicationAnnotations.TryGetValue(ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(sourceName), out var environmentAnnotation);
            applicationAnnotations.TryGetValue(ArgoCDConstants.Annotations.OctopusTenantAnnotationKey(sourceName), out var tenantAnnotation);
            
            return (
                projectAnnotation.ToProjectSlug(), 
                environmentAnnotation.ToEnvironmentSlug(),
                tenantAnnotation.ToTenantSlug()
            );
        }
    }
}
#endif