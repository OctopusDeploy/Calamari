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
        public static (ProjectSlug? Project, EnvironmentSlug? Environment, TenantSlug? Tenant) ReadScopeMapping(IReadOnlyDictionary<string, string> applicationAnnotations, string sourceName)
        {
            applicationAnnotations.TryGetValue(ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(sourceName), out var projectAnnotation);
            applicationAnnotations.TryGetValue(ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(sourceName), out var environmentAnnotation);
            applicationAnnotations.TryGetValue(ArgoCDConstants.Annotations.OctopusTenantAnnotationKey(sourceName), out var tenantAnnotation);
            
            return (
                string.IsNullOrWhiteSpace(projectAnnotation) ? null : new ProjectSlug(projectAnnotation), 
                string.IsNullOrWhiteSpace(environmentAnnotation) ? null : new EnvironmentSlug(environmentAnnotation),
                string.IsNullOrWhiteSpace(tenantAnnotation) ? null : new TenantSlug(tenantAnnotation)
            );
        }
    }
}
#endif