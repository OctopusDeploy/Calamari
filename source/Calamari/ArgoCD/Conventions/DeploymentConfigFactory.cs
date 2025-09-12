#if NET
using System;
using System.Linq;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;
using Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions
{
    public class DeploymentConfigFactory
    {
        readonly INonSensitiveVariables nonSensitiveVariables;

        public DeploymentConfigFactory(INonSensitiveVariables nonSensitiveVariables)
        {
            this.nonSensitiveVariables = nonSensitiveVariables;
        }

        public ArgoCommitToGitConfig Create(RunningDeployment deployment)
        {
            var inputPath = deployment.Variables.Get(SpecialVariables.Git.InputPath, string.Empty);
            var recursive = deployment.Variables.GetFlag(SpecialVariables.Git.Recursive, false);
            var commitParameters = CommitParameters(deployment);
            
            return new ArgoCommitToGitConfig(
                                           deployment.CurrentDirectory,
                                           inputPath,
                                           recursive,
                                           commitParameters);
        }

        public UpdateArgoCDAppDeploymentConfig CreateOther(RunningDeployment deployment)
        {
            var commitParameters = CommitParameters(deployment);
            var packageReferences = deployment.Variables.GetContainerPackageNames().Select(p => ContainerImageReference.FromReferenceString(p)).ToList();
            return new UpdateArgoCDAppDeploymentConfig(commitParameters, packageReferences);
        }
        
        bool RequiresPullRequest(RunningDeployment deployment)
        {
            return OctopusFeatureToggles.ArgoCDCreatePullRequestFeatureToggle.IsEnabled(deployment.Variables) && deployment.Variables.Get(SpecialVariables.Git.CommitMethod) == SpecialVariables.Git.GitCommitMethods.PullRequest;
        }

        GitCommitParameters CommitParameters(RunningDeployment deployment)
        {
            var requiresPullRequest = RequiresPullRequest(deployment);
            // TODO #project-argo-cd-in-octopus: put both types of variables on RunningDeployment and encapsulate so the dev thinks about whether
            // the variable is sensitive
            var summary = EvaluateNonsensitiveExpression(nonSensitiveVariables.GetMandatoryVariableRaw(SpecialVariables.Git.CommitMessageSummary));
            var description = EvaluateNonsensitiveExpression(nonSensitiveVariables.GetRaw(SpecialVariables.Git.CommitMessageDescription) ?? string.Empty);
            return new GitCommitParameters(summary, description, requiresPullRequest);
        }
        string EvaluateNonsensitiveExpression(string expression)
        {
            var result = nonSensitiveVariables.Evaluate(expression, out string error);
                
            //We always want to throw when substitution fails
            if (!string.IsNullOrEmpty(error))
            {
                var message = $"Parsing variable with Octostache returned the following error: `{error}`";
                throw new CommandException($"{message}. This may be due to missing or sensitive variables.");
            }

            return result;
        }
    }
}
#endif