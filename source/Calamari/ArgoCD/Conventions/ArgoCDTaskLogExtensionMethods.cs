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
    }
}
#endif