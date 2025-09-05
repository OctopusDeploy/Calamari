using System.Collections.Generic;
using Calamari.ArgoCD.Git;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions
{
    public class ArgoCommitToGitConfigFactory
    {
        readonly ILog log;

        public ArgoCommitToGitConfigFactory(ILog log)
        {
            this.log = log;
        }

        public ArgoCommitToGitConfig Create(RunningDeployment deployment)
        {
            var inputPath = deployment.Variables.Get(SpecialVariables.Git.InputPath, string.Empty);
            var recursive = deployment.Variables.GetFlag(SpecialVariables.Git.Recursive, false);
            
            var requiresPullRequest = RequiresPullRequest(deployment);
            var summary = deployment.Variables.GetMandatoryVariable(SpecialVariables.Git.CommitMessageSummary);
            var description = deployment.Variables.Get(SpecialVariables.Git.CommitMessageDescription) ?? string.Empty;
            
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
