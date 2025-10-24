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

        public static void LogMissingAnnotationsWarning(this ILog log, (ProjectSlug Project, EnvironmentSlug Environment, TenantSlug? Tenant) deploymentScope)
        {
            log.Warn("No annotated Argo applications could be found for this deployment");
            log.Warn("Please ensure application to be updated has been annotated with:");
            log.WarnFormat(" - {0}: {1}", ArgoCDConstants.Annotations.OctopusProjectAnnotationKey(".<sourcename>".ToApplicationSourceName()), deploymentScope.Project);
            log.WarnFormat(" - {0}: {1}", ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(".<sourcename>".ToApplicationSourceName()), deploymentScope.Environment);
            if (deploymentScope.Tenant != null)
            {
                log.WarnFormat(" - {0}: {1}", ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey(".<sourcename>".ToApplicationSourceName()), deploymentScope.Tenant);
            }
        }
    }
}
#endif