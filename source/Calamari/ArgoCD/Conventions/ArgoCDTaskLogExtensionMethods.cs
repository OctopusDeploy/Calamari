#nullable enable
using System;
using Calamari.ArgoCD.Models;
using Calamari.Common.Plumbing.Logging;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Conventions
{
    public static class ArgoCDTaskLogExtensionMethods
    {
        public static void LogApplicationSourceScopeStatus(this ILog log, AnnotationScope annotatedScope, ApplicationSourceName? sourceName, DeploymentScope deploymentScope)
        {
            log.Verbose($"Application source scopes are Project: '{annotatedScope.Project}', Environment: '{annotatedScope.Environment}', Tenant: '{annotatedScope.Tenant}', Step: '{annotatedScope.Step}'");
            string applicationNameInLogs = sourceName == null ? "(unnamed)" : $"'{sourceName.Value}'";
            if (deploymentScope.Matches(annotatedScope))
            {
                log.Info($"Updating application source {applicationNameInLogs}");
            }
            else if (annotatedScope.Step != null && deploymentScope.Step != null)
            {
                log.Verbose($"Not updating application source {applicationNameInLogs} because it is annotated for step '{annotatedScope.Step}', not '{deploymentScope.Step}'");
            }
            else
            {
                log.Verbose($"Not updating application source {applicationNameInLogs} because it's not associated with this deployment");
            }
        }

        static void LogMissingAnnotationsWarning(this ILog log, DeploymentScope deploymentScope)
        {
            log.Warn("No annotated Argo CD applications could be found for this deployment.");
            log.Warn("Please annotate your application(s) with the following to allow deployments to find and update them:");
            log.WarnFormat(" - {0}: {1}", ArgoCDConstants.Annotations.OctopusProjectAnnotationKey("<sourcename>".ToApplicationSourceName()), deploymentScope.Project);
            log.WarnFormat(" - {0}: {1}", ArgoCDConstants.Annotations.OctopusEnvironmentAnnotationKey("<sourcename>".ToApplicationSourceName()), deploymentScope.Environment);
            if (deploymentScope.Tenant != null)
            {
                log.WarnFormat(" - {0}: {1}", ArgoCDConstants.Annotations.OctopusTenantAnnotationKey("<sourcename>".ToApplicationSourceName()), deploymentScope.Tenant);
            }
            if (deploymentScope.Step != null)
            {
                log.WarnFormat(" - {0}: {1} (optional, only needed to reserve a source for one step)", ArgoCDConstants.Annotations.OctopusStepAnnotationKey("<sourcename>".ToApplicationSourceName()), deploymentScope.Step);
            }
            log.WarnFormat("Annotation creation documentation can be found {0}.", log.FormatShortLink("argo-cd-annotations-docs", "here"));
        }
        
        public static void LogApplicationCounts(this ILog log, DeploymentScope deploymentScope, ArgoCDApplicationDto[] applications)
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
