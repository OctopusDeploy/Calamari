using System.Collections.Generic;
using Calamari.ArgoCD.Git;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions
{
    public class ArgoCommitToGitSpecFactory
    {
        readonly ILog log;

        public ArgoCommitToGitSpecFactory(ILog log)
        {
            this.log = log;
        }

        public ArgoCommitToGitSpec Create(RunningDeployment deployment)
        {
            var inputPath = deployment.Variables.GetMandatoryVariable(SpecialVariables.Git.InputPath);
            var recursive = deployment.Variables.GetFlag(SpecialVariables.Git.Recursive, false);
            
            var requiresPullRequest = RequiresPullRequest(deployment);
            var summary = deployment.Variables.GetMandatoryVariable(SpecialVariables.Git.CommitMessageSummary);
            var description = deployment.Variables.Get(SpecialVariables.Git.CommitMessageDescription) ?? string.Empty;
            var purgeOutput = deployment.Variables.GetFlag(SpecialVariables.Git.PurgeOutput);
            
            var repositoryIndexes = deployment.Variables.GetIndexes(SpecialVariables.Git.Index);
            log.Info($"Found the following repository indicies '{repositoryIndexes.Join(",")}'");
            List<IArgoApplicationSource> gitConnections = new List<IArgoApplicationSource>();
            foreach (var repositoryIndex in repositoryIndexes)
            {
                gitConnections.Add(new VariableBackedArgoSource(deployment.Variables, repositoryIndex));
            }

            return new ArgoCommitToGitSpec(
                                           deployment.CurrentDirectory,
                                           inputPath,
                                           recursive,
                                           summary,
                                           description,
                                           requiresPullRequest,
                                           purgeOutput,
                                           gitConnections);
        }
        
        bool RequiresPullRequest(RunningDeployment deployment)
        {
            return OctopusFeatureToggles.ArgoCDCreatePullRequestFeatureToggle.IsEnabled(deployment.Variables) && deployment.Variables.Get(SpecialVariables.Git.CommitMethod) == SpecialVariables.Git.GitCommitMethods.PullRequest;
        }
    }
}