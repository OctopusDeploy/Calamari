#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
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

        static void LogMissingAnnotationsWarning(this ILog log, (ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant) deploymentScope)
        {
            log.Warn("No annotated Argo CD applications could be found for this deployment.");
            log.Warn("Please annotate your application(s) with the following to allow deployments to find and update them:");
            log.WarnFormat(" - {0}: {1}", ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("<sourcename>".ToApplicationSourceName()), deploymentScope.Project);
            log.WarnFormat(" - {0}: {1}", ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("<sourcename>".ToApplicationSourceName()), deploymentScope.Environment);
            if (deploymentScope.Tenant != null)
            {
                log.WarnFormat(" - {0}: {1}", ArgoCDConstants.Annotations.OctopusTenantAnnotationKey("<sourcename>".ToApplicationSourceName()), deploymentScope.Tenant);
            }
            log.WarnFormat("Annotation creation documentation can be found {0}.", log.FormatShortLink("argo-cd-annotations-docs", "here"));
        }
        
        public static void LogApplicationCounts(this ILog log, (ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant) deploymentScope, ArgoCDApplicationDto[] applications)
        {
            if (applications.Length == 0)
            {
                log.LogMissingAnnotationsWarning(deploymentScope);
            }
            else
            {
                log.InfoFormat("Found {0} Argo CD applications to update", applications.Length);
                foreach (var app in applications)
                {
                    log.VerboseFormat("- {0}", app.Name);
                }
            }
        }
    }
}
