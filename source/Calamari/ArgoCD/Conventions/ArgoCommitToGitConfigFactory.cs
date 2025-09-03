using System.Collections.Generic;
using Amazon.CloudFormation.Model;
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
            
            //These two are problematic as they MAY be sensitive
            var summary = GetNonSensitiveVariable(SpecialVariables.Git.CommitMessageSummary);
            var description = GetNonSensitiveVariable(SpecialVariables.Git.CommitMessageDescription) ?? string.Empty;
            
            var repositoryIndexes = deployment.Variables.GetIndexes(SpecialVariables.Git.Index);
            log.Info($"Found the following repository indicies '{repositoryIndexes.Join(",")}'");
            List<IArgoApplicationSource> gitConnections = new List<IArgoApplicationSource>();
            foreach (var repositoryIndex in repositoryIndexes)
            {
                gitConnections.Add(new VariableBackedArgoSource(deployment.Variables, repositoryIndex));
            }

            return new ArgoCommitToGitConfig(
                                           deployment.CurrentDirectory,
                                           inputPath,
                                           recursive,
                                           summary,
                                           description,
                                           requiresPullRequest,
                                           gitConnections);
        }
        
        bool RequiresPullRequest(RunningDeployment deployment)
        {
            return OctopusFeatureToggles.ArgoCDCreatePullRequestFeatureToggle.IsEnabled(deployment.Variables) && deployment.Variables.Get(SpecialVariables.Git.CommitMethod) == SpecialVariables.Git.GitCommitMethods.PullRequest;
        }

        string GetNonSensitiveVariable(string variableName, RunningDeployment deployment)
        {
            var result = deployment.Variables.Get(variableName, out var error);
            if (!string.IsNullOrEmpty(error))
            {
                var message = $"Unable to evaluate '{variableName}'. It may reference missing or sensitive values.";
                throw new InvalidOperationException(message);
            }

            return result;
        }
        
        string GetMandatoryNonSensitiveVariable(string variableName, RunningDeployment deployment)
        {
            var result = deployment.Variables.Get(variableName, out var error) ?? null;
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new CommandException($"Variable {variableName} was not supplied");
            }
            
            if (!string.IsNullOrEmpty(error))
            {
                var message = $"Unable to evaulate '{variableName}'. It may reference missing or sensitive values.";
                throw new InvalidOperationException(message);
            }

            return result;
        }
    }
}
