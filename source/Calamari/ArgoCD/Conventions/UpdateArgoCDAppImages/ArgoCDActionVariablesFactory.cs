using System;
using System.Linq;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{
    public class ArgoCDActionVariablesFactory
    {
        public ArgoCDUpdateActionVariables CreateArgoCDUpdateActionVariables(IVariables variables, bool pullRequestsFeatureEnabled = false)
        {
            var baseVariables = CreateArgoCDActionVariables(variables, pullRequestsFeatureEnabled);
            
            var packageReferences = variables.GetContainerPackageNames().Select(p => ContainerImageReference.FromReferenceString(p)).ToList();

            return new ArgoCDUpdateActionVariables(baseVariables, packageReferences);
        }
        
        public ArgoCDActionVariablesBase CreateArgoCDActionVariables(IVariables variables, bool pullRequestsFeatureEnabled = false)
        {
            // var projectSlug = variables.Get(SpecialVariables.Project.Slug);
            // var environmentSlug = variables.Get(SpecialVariables.Environment.Slug);
            // var tenantSlug = variables.Get(SpecialVariables.Deployment.Tenant.Slug);
            //
            // if (string.IsNullOrWhiteSpace(projectSlug) || string.IsNullOrWhiteSpace(environmentSlug))
            // {
            //     throw new ActivityFailedException("Project or Environment slug is not set in Process Variables. Cannot proceed with Argo CD Update Action.");
            // }

            var commitMessageSummary = variables.Get(SpecialVariables.Git.CommitMessageSummary) ?? throw new CommandException("Git commit summary is not set.");
            var commitMessageDescription = variables.Get(SpecialVariables.Git.CommitMessageDescription);
            var commitMethod = variables.Get(SpecialVariables.Git.CommitMethod) ?? SpecialVariables.Action.ArgoCD.GitCommitMethods.DirectCommit;

            return new ArgoCDActionVariablesBase(projectSlug, environmentSlug, tenantSlug, new GitCommitSummary(commitMessageSummary),
                commitMessageDescription, pullRequestsFeatureEnabled && commitMethod.Equals(SpecialVariables.Action.ArgoCD.GitCommitMethods.PullRequest));
        }
    }
}
