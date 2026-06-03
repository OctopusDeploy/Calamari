using System;
using Calamari.ArgoCD.Git;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.CommitToGit;
using Calamari.Deployment;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Calamari.Contracts.CommitToGit;
using Octopus.Calamari.Contracts.Git;

namespace Calamari.Tests.CommitToGit;

[TestFixture]
public class CommitToGitConfigFactoryTests
{
    INonSensitiveVariables nonSensitiveVariables;
    IVariables variables;
    ICustomPropertiesLoader loader;
    CommitToGitConfigFactory factory;

    [SetUp]
    public void SetUp()
    {
        nonSensitiveVariables = Substitute.For<INonSensitiveVariables>();
        variables = Substitute.For<IVariables>();
        loader = Substitute.For<ICustomPropertiesLoader>();
        factory = new CommitToGitConfigFactory(nonSensitiveVariables);

        variables.Get(SpecialVariables.Action.Git.Url).Returns("https://example.invalid/repo.git");
        variables.Get(SpecialVariables.Action.Git.Reference).Returns("refs/heads/main");
        nonSensitiveVariables.GetMandatoryVariableRaw(SpecialVariables.Action.Git.CommitMessageSummary)
                             .Returns("summary");
        nonSensitiveVariables.GetRaw(SpecialVariables.Action.Git.CommitMessageDescription)
                             .Returns(string.Empty);
    }

    [Test]
    public void CreateRepositoryConfig_UsesUsernameAndPasswordFromLoadedProperties()
    {
        loader.Load<CommitToGitCustomPropertiesDto>()
              .Returns(new CommitToGitCustomPropertiesDto(new UsernamePasswordGitCredentialDto("MyCred", "https://example.invalid/repo.git", "user-from-file", "pwd-from-file")));

        var deployment = new RunningDeployment(null, variables);

        var config = factory.CreateRepositoryConfig(deployment, loader);

        var httpsGitConnection = config.GitConnection as HttpsGitConnection;
        httpsGitConnection.Should().NotBeNull();
        httpsGitConnection!.Username.Should().Be("user-from-file");
        httpsGitConnection.Password.Should().Be("pwd-from-file");
        httpsGitConnection.Uri.Value.Should().Be(new Uri("https://example.invalid/repo.git"));
    }

    [Test]
    public void CreateRepositoryConfig_WhenCreatingPullRequestWithSshKeyCredential_Throws()
    {
        variables.GetFlag(SpecialVariables.Action.Git.PullRequest.Create).Returns(true);
        loader.Load<CommitToGitCustomPropertiesDto>()
              .Returns(new CommitToGitCustomPropertiesDto(new SshKeyGitCredentialDto("MyCred", "https://example.invalid/repo.git", "git", "private-key", [])));

        var deployment = new RunningDeployment(null, variables);

        var act = () => factory.CreateRepositoryConfig(deployment, loader);

        act.Should().Throw<CommandException>().And.Message.Should().Contain("SSH key authentication");
    }

    [Test]
    public void CreateRepositoryConfig_WhenCreatingPullRequestWithoutCredentials_Throws()
    {
        variables.GetFlag(SpecialVariables.Action.Git.PullRequest.Create).Returns(true);
        loader.Load<CommitToGitCustomPropertiesDto>()
              .Returns(new CommitToGitCustomPropertiesDto(null));

        var deployment = new RunningDeployment(null, variables);

        var act = () => factory.CreateRepositoryConfig(deployment, loader);

        act.Should().Throw<CommandException>().And.Message.Should().Contain("requires Git repository credentials");
    }

    [Test]
    public void CreateRepositoryConfig_WhenDestinationPathIsMissing_DefaultsToEmptyString()
    {
        //Octopus server removes variables containing empty strings, thus a missing property should default to an empty string.
        //Thus the TargetRepositorydestinationPath could validly be missing from the variable set, in such case, it should default to an empty string.
        loader.Load<CommitToGitCustomPropertiesDto>()
              .Returns(new CommitToGitCustomPropertiesDto(new UsernamePasswordGitCredentialDto("MyCred", "https://example.invalid/repo.git", "user", "pwd")));
        variables.Get(SpecialVariables.Action.Git.DestinationPath).Returns((string)null);

        var deployment = new RunningDeployment(null, variables);

        var config = factory.CreateRepositoryConfig(deployment, loader);

        config.DestinationPath.Should().Be(string.Empty);
    }
}
