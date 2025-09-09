using System.Collections.Generic;
using Calamari.ArgoCD.Git;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions
{
    public class ArgoCommitToGitConfigFactory
    {
        readonly INonSensitiveVariables nonSensitiveVariables;

        public ArgoCommitToGitConfigFactory(INonSensitiveVariables nonSensitiveVariables)
        {
            this.nonSensitiveVariables = nonSensitiveVariables;
        }

        public ArgoCommitToGitConfig Create(RunningDeployment deployment)
        {
            var inputPath = deployment.Variables.Get(SpecialVariables.Git.InputPath, string.Empty);
            var recursive = deployment.Variables.GetFlag(SpecialVariables.Git.Recursive, false);
            
            var requiresPullRequest = RequiresPullRequest(deployment);

            // TODO #project-argo-cd-in-octopus: put both types of variables on RunningDeployment and encapsulate so the dev thinks about whether
            // the variable is sensitive
            var summary = nonSensitiveVariables.GetMandatoryVariable(SpecialVariables.Git.CommitMessageSummary);
            var description = nonSensitiveVariables.Get(SpecialVariables.Git.CommitMessageDescription) ?? string.Empty;
            
            return new ArgoCommitToGitConfig(
                                           deployment.CurrentDirectory,
                                           inputPath,
                                           recursive,
                                           summary,
                                           description,
                                           requiresPullRequest);
        }
        
        bool RequiresPullRequest(RunningDeployment deployment)
        {
            return OctopusFeatureToggles.ArgoCDCreatePullRequestFeatureToggle.IsEnabled(deployment.Variables) && deployment.Variables.Get(SpecialVariables.Git.CommitMethod) == SpecialVariables.Git.GitCommitMethods.PullRequest;
        }
    }
}
