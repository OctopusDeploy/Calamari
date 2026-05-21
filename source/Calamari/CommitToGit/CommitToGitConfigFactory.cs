using System;
using Amazon.ECS.Model;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Octopus.Calamari.Contracts.CommitToGit;
using Octopus.Calamari.Contracts.Git;

namespace Calamari.CommitToGit
{
    public class CommitToGitConfigFactory
    {
        readonly INonSensitiveVariables nonSensitiveVariables;

        public CommitToGitConfigFactory(INonSensitiveVariables nonSensitiveVariables)
        {
            this.nonSensitiveVariables = nonSensitiveVariables;
        }

        public CommitToGitRepositorySettings CreateRepositoryConfig(RunningDeployment deployment, ICustomPropertiesLoader customPropertiesLoader)
        {
            var variables = deployment.Variables;

            var uriAsString = variables.Get(SpecialVariables.Action.Git.Url)
                              ?? throw new CommandException($"Required variable '{SpecialVariables.Action.Git.Url}' is not set.");

            var gitReferenceAsString = variables.Get(SpecialVariables.Action.Git.Reference)
                                       ?? throw new CommandException($"Required variable '{SpecialVariables.Action.Git.Reference}' is not set.");

            var requiresPullRequest = variables.GetFlag(SpecialVariables.Action.Git.PullRequest.Create);
            var summary = EvaluateNonsensitiveExpression(nonSensitiveVariables.GetMandatoryVariableRaw(SpecialVariables.Action.Git.CommitMessageSummary));
            var description = EvaluateNonsensitiveExpression(nonSensitiveVariables.GetRaw(SpecialVariables.Action.Git.CommitMessageDescription) ?? string.Empty);
            var commitParameters = new GitCommitParameters(summary, description, requiresPullRequest);

            var properties = customPropertiesLoader.Load<CommitToGitCustomPropertiesDto>();

            IGitConnection connection = properties.GitCredential switch
                                        {
                                            UsernamePasswordGitCredentialDto usernamePassword => new HttpsGitConnection(usernamePassword.Username, usernamePassword.Password, uriAsString, GitReference.CreateFromString(gitReferenceAsString)),
                                            SshKeyGitCredentialDto ssh => new SshKeyGitConnection(ssh.Username, ssh.PrivateKey, uriAsString, GitReference.CreateFromString(gitReferenceAsString)),
                                            _ => throw new NotSupportedException($"An unrecognised credential type '{properties.GitCredential.GetType().Name}' was found for '{uriAsString}'"),
                                        };

            return new CommitToGitRepositorySettings(connection,
                                                     commitParameters,
                                                     variables.Get(SpecialVariables.Action.Git.DestinationPath));
        }

        string EvaluateNonsensitiveExpression(string expression)
        {
            var result = nonSensitiveVariables.Evaluate(expression, out string error);

            if (!string.IsNullOrEmpty(error))
            {
                var message = $"Parsing variable with Octostache returned the following error: `{error}`";
                throw new CommandException($"{message}. This may be due to missing or sensitive variables.");
            }

            return result;
        }
    }
}