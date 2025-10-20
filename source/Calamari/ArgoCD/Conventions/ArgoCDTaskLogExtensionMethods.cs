#if NET
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Conventions
{
    public static class ArgoCDTaskLogExtensionMethods
    {
        public static void LogApplicationSourceScopeStatus(this ILog log, (ProjectSlug? Project, EnvironmentSlug? Environment, TenantSlug? Tenant) annotatedScope, ApplicationSourceName? sourceName, (ProjectSlug? Project, EnvironmentSlug? Environment, TenantSlug? Tenant) deploymentScope)
        {
            log.Verbose($"Application source scopes are Project: '{annotatedScope.Project}', Environment: '{annotatedScope.Environment}', Tenant: '{annotatedScope.Tenant}'");
            string applicationNameInLogs = sourceName == null ? "(unnamed)" : $"'{sourceName.Value}'";
            if (annotatedScope == deploymentScope)
            {
                log.Info($"Updating application source {applicationNameInLogs}");
            }
            else
            {
                log.Verbose($"Not updating application source {applicationNameInLogs} because it's not associated with this deployment");
            }
        }

        public static void LogUnnamedAnnotationsInMultiSourceApplication(this ILog log, Application application)
        {
            if (application.Spec.Sources.Count <= 1)
                return;
            
            var unnamedAnnotations = ArgoCDConstants.Annotations.GetUnnamedAnnotationKeys()
                                                    .Where(application.Metadata.Annotations.ContainsKey)
                                                    .ToArray();

            if (unnamedAnnotations.Any())
            {
                log.Warn($"The application '{application.Metadata.Name}' requires all annotations to be qualified by source name since it contains multiple sources. Found these unqualified annotations: {string.Join(", ", unnamedAnnotations)}.");
            }
        }
    }
}
#endif