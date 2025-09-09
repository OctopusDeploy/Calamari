using System.Collections.Generic;
using Calamari.ArgoCD.Git;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions
{
    public class ArgoCommitToGitConfigFactory
    {
        readonly ILog log;
        readonly INonSensitiveVariables nonSensitiveVariables;

        public ArgoCommitToGitConfigFactory(ILog log, INonSensitiveVariables nonSensitiveVariables)
        {
            this.log = log;
            this.nonSensitiveVariables = nonSensitiveVariables;
        }

        public ArgoCommitToGitConfig Create(RunningDeployment deployment)
        {
            var inputPath = deployment.Variables.Get(SpecialVariables.Git.InputPath, string.Empty);
            var recursive = deployment.Variables.GetFlag(SpecialVariables.Git.Recursive, false);
            
            var requiresPullRequest = RequiresPullRequest(deployment);

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
