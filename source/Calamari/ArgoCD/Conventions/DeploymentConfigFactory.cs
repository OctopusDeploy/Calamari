using System;
using System.Linq;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Models;
using Calamari.CommitToGit;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Conventions
{
    public class DeploymentConfigFactory
    {
        readonly INonSensitiveVariables nonSensitiveVariables; 

        public DeploymentConfigFactory(INonSensitiveVariables nonSensitiveVariables)
        {
            this.nonSensitiveVariables = nonSensitiveVariables;
        }

        public ArgoCommitToGitConfig CreateCommitToGitConfig(RunningDeployment deployment)
        {
            var commitParameters = CommitParameters(deployment);
            var inputPath = deployment.Variables.Get(SpecialVariables.Git.InputPath, string.Empty);
            var purgeOutput = deployment.Variables.GetFlag(SpecialVariables.Git.PurgeOutput, false);
            
            return new ArgoCommitToGitConfig(
                                           deployment.CurrentDirectory,
                                           inputPath,
                                           purgeOutput,
                                           commitParameters);
        }

        public UpdateArgoCDAppDeploymentConfig CreateUpdateImageConfig(RunningDeployment deployment)
        {
            var commitParameters = CommitParameters(deployment);
            var packageHelmReference = deployment.Variables.GetContainerPackages().Select(p => new ContainerImageReferenceAndHelmReference(ContainerImageReference.FromReferenceString(p.PackageName),
                                                                                                                          p.HelmReference)).ToList();
            var useHelmReferenceFromContainer = OctopusFeatureToggles.ArgoCDHelmReplacePathFromContainerReferenceFeatureToggle.IsEnabled(deployment.Variables);
            var updateKustomizePatches = OctopusFeatureToggles.KustomizePatchImageUpdatesFeatureToggle.IsEnabled(deployment.Variables);
            return new UpdateArgoCDAppDeploymentConfig(commitParameters, packageHelmReference, useHelmReferenceFromContainer, updateKustomizePatches);
        }
        
        bool RequiresPullRequest(RunningDeployment deployment)
        {
            return deployment.Variables.GetFlag(SpecialVariables.Git.PullRequest.Create);
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
        
        public CommitToGitRepositorySettings CreateCommitToGitRepositoryConfig(RunningDeployment deployment)
        {
            var variables = deployment.Variables;

            var uriAsString = variables.Get(Deployment.SpecialVariables.Action.Git.Url);
            
            var gitReferenceAsString = variables.Get(Deployment.SpecialVariables.Action.Git.Reference);
        
            return new CommitToGitRepositorySettings(
                                                     new GitConnection(
                                                                       variables.Get(Deployment.SpecialVariables.Action.Git.Username),
                                                                       variables.Get(Deployment.SpecialVariables.Action.Git.Password),
                                                                       new Uri(uriAsString),
                                                                       GitReference.CreateFromString(gitReferenceAsString)),
            CommitParameters(deployment),
            variables.Get(Deployment.SpecialVariables.Action.Git.DestinationPath));

        } 
    }
}

