using System;
using System.Collections;
using System.Threading;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Deployment.Conventions;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppImagesInstallConvention : IInstallConvention
    {
        readonly ILog log;
        readonly IGitHubPullRequestCreator pullRequestCreator;

        public UpdateArgoCDAppImagesInstallConvention(ILog log, IGitHubPullRequestCreator pullRequestCreator)
        {
            this.log = log;
            this.pullRequestCreator = pullRequestCreator;
        }

        public void Install(RunningDeployment deployment)
        {
            var repositoryFactory = new RepositoryFactory(log, deployment.CurrentDirectory, pullRequestCreator);
            var requiresPullRequest = RequiresPullRequest(deployment);

            var stepVariableFactory = new ArgoCDActionVariablesFactory();
            var stepVariables = stepVariableFactory.CreateArgoCDUpdateActionVariables(deployment.Variables, pullRequestEnabled);

            var repositoryIndexes = deployment.Variables.GetIndexes(SpecialVariables.Git.Index);
            log.Info($"Found the following repository indicies '{repositoryIndexes.Join(",")}'");
            foreach (var repositoryIndex in repositoryIndexes)
            {
                Log.Info($"Writing files to repository for '{repositoryIndex}'");
                IGitConnection gitConnection = new VariableBackedGitConnection(deployment.Variables, repositoryIndex);
                var repository = repositoryFactory.CloneRepository(repositoryIndex, gitConnection);

                //Do the package based logic here

                Log.Info("Staging files in repository");
                repository.StageFiles(Array.Empty<string>());

                Log.Info("Commiting changes");
                var commitMessage = GenerateCommitMessage(deployment);
                if (repository.CommitChanges(commitMessage))
                {
                    Log.Info("Changes were commited, pushing to remote");
                    repository.PushChanges(requiresPullRequest, gitConnection.BranchName, CancellationToken.None).GetAwaiter().GetResult();
                }
                else
                {
                    Log.Info("No changes were commited.");
                }
            }
        }

        bool RequiresPullRequest(RunningDeployment deployment)
        {
            return OctopusFeatureToggles.ArgoCDCreatePullRequestFeatureToggle.IsEnabled(deployment.Variables) && deployment.Variables.Get(SpecialVariables.Git.CommitMethod) == SpecialVariables.Git.GitCommitMethods.PullRequest;
        }

        string GenerateCommitMessage(RunningDeployment deployment)
        {
            return "this is a commit message";
        }

        IEnumerable<
    }
}